using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Monitors;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary> Encoder listesinin bir satırı (bir aday, tüm monitörlerin sınama sonucu birleşik). </summary>
public sealed class EncoderRowVM
{
    public required string Symbol { get; init; }
    public required Brush SymbolBrush { get; init; }
    public required string Encoder { get; init; }
    public required string Chain { get; init; }
    public required string Detail { get; init; }
    public required bool IsSelected { get; init; }
    /// <summary> En az bir monitörde çalışan ve şu an seçili olmayan aday tercih edilebilir. </summary>
    public required bool CanPrefer { get; init; }
    public required ICommand PreferCommand { get; init; }
}

/// <summary>
/// "Encoder" kutucuğu — monitörlerden ayrı: sınama (tüm monitörler), aday listesi ve PC başına tercih (bellekte).
/// Sınama monitör başına yapılır (yakalama monitörün ekran kartına bağlı); liste adayları birleştirir ve bir aday bazı monitörlerde
/// çalışmıyorsa bunu satırda söyler. Servis olayları iş parçacığı havuzundan gelir, burada Dispatcher'a taşınır.
/// </summary>
public sealed class EncoderPanelVM : BaseViewModel
{
    private static readonly Brush Ok = Freeze(Color.FromRgb(0x2E, 0x9E, 0x5B));
    private static readonly Brush Bad = Freeze(Color.FromRgb(0xD1, 0x43, 0x43));
    private static readonly Brush Warn = Freeze(Color.FromRgb(0xC7, 0x8A, 0x10));

    private readonly IEncoderProbeService _probes;
    private readonly IMonitorService _monitors;

    public EncoderPanelVM(IEncoderProbeService probes, IMonitorService monitors)
    {
        _probes = probes;
        _monitors = monitors;

        ProbeCommand = new RelayCommand(async _ => await ProbeAllAsync(), _ => !IsBusy);
        ResetPreferenceCommand = new RelayCommand(_ => _probes.SetPreferred(null), _ => _probes.Preferred is not null);

        _probes.PreferenceChanged += () => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        // İlk izlemede kendiliğinden yapılan sınama da listeye yansısın.
        _probes.Probed += () => Application.Current?.Dispatcher.BeginInvoke(Refresh);

        Refresh();
    }

    public ICommand ProbeCommand { get; }
    public ICommand ResetPreferenceCommand { get; }

    public ObservableCollection<EncoderRowVM> Rows { get; } = [];

    private string _summary = "";
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) ((RelayCommand)ProbeCommand).RaiseCanExecuteChanged(); } }

    public bool HasPreference => _probes.Preferred is not null;

    public string PreferenceText => _probes.Preferred is { } kind
        ? $"Tercih: {EncoderCatalog.Find(kind).DisplayName} — bu PC'deki tüm yayınlar bunu kullanır; çalışmadığı monitörde otomatik seçim. " +
          "Uygulama kapanınca otomatiğe döner."
        : "Tercih: otomatik (öncelik sırası). Değiştirmek için listede \"Tercih et\".";

    /// <summary> Monitör listesi değişince (<see cref="HomeVM"/>) ve servis olaylarında çağrılır. </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(HasPreference));
        OnPropertyChanged(nameof(PreferenceText));
        ((RelayCommand)ResetPreferenceCommand).RaiseCanExecuteChanged();
        if (!IsBusy) ShowProbes();
    }

    private async Task ProbeAllAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<MonitorInfo> monitors = _monitors.GetMonitors();
            foreach (var monitor in monitors)
            {
                Summary = $"Sınanıyor… Monitör {monitor.Index} ({monitors.Count} monitörden) — her aday birkaç kare kodluyor.";
                await _probes.ProbeAsync(monitor, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Summary = $"Sınama yapılamadı: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }
        ShowProbes();
    }

    private void ShowProbes()
    {
        Rows.Clear();

        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = _monitors.GetMonitors(); }
        catch (Exception ex) { Summary = $"Monitörler okunamadı: {ex.Message}"; return; }

        var probes = monitors.Select(m => _probes.GetCached(m.Index)).OfType<MonitorProbe>().ToList();
        if (probes.Count == 0)
        {
            Summary = "Henüz sınanmadı — \"Encoder'ları sına\" ya da ilk izlemede kendiliğinden sınanır.";
            return;
        }

        // Sıra: birincil monitörün öncelik sırası (yoksa ilk sınanan); diğer monitörlerde olup onda olmayan adaylar sona.
        var primary = probes.FirstOrDefault(p => p.Monitor.IsPrimary) ?? probes[0];
        var candidates = primary.Results.Select(r => r.Candidate)
            .Concat(probes.SelectMany(p => p.Results.Select(r => r.Candidate)))
            .DistinctBy(c => c.Kind)
            .ToList();

        foreach (var candidate in candidates)
        {
            var results = probes.Select(p => (p.Monitor, Result: p.Results.FirstOrDefault(r => r.Candidate.Kind == candidate.Kind)))
                .Where(x => x.Result is not null)
                .ToList();
            var working = results.Where(x => x.Result!.Ok).ToList();
            var failing = results.Where(x => !x.Result!.Ok).ToList();

            bool isSelected = probes.All(p => p.Selected?.Candidate.Kind == candidate.Kind);
            (string symbol, Brush brush) = (working.Count, failing.Count) switch
            {
                (> 0, 0) => ("✓", Ok),
                (0, _) => ("✗", Bad),
                _ => ("◐", Warn),
            };
            string detail = failing.Count == 0
                ? $"çalışıyor ({working.Max(x => x.Result!.Duration).TotalSeconds:0.0} sn)"
                : working.Count == 0
                    ? failing[0].Result!.Reason ?? ""
                    : string.Join("; ", failing.Select(x => $"Monitör {x.Monitor.Index}'de çalışmadı: {x.Result!.Reason}"));

            EncoderKind kind = candidate.Kind;
            Rows.Add(new EncoderRowVM
            {
                Symbol = symbol,
                SymbolBrush = brush,
                Encoder = $"{candidate.DisplayName} · {CodecText(candidate.Codec)}",
                Chain = candidate.Chain,
                Detail = detail,
                IsSelected = isSelected,
                CanPrefer = working.Count > 0 && !isSelected,
                // PC başına tercih: bu PC'deki tüm yayınlar (merkez dahil) bu adayı kullanır, çalışan yayınlar yeniden başlar.
                PreferCommand = new RelayCommand(_ => _probes.SetPreferred(kind)),
            });
        }

        Summary = BuildSummary(probes, monitors.Count);
    }

    private static string BuildSummary(IReadOnlyList<MonitorProbe> probes, int monitorCount)
    {
        string at = $"({probes.Max(p => p.ProbedAt):HH:mm:ss})";
        string notProbed = probes.Count < monitorCount ? $" · {monitorCount - probes.Count} monitör henüz sınanmadı" : "";

        var selections = probes.Select(p => (p.Monitor.Index, p.Selected, p.IsPreferenceApplied, p.PreferenceUnavailable)).ToList();
        if (selections.All(s => s.Selected is null))
            return $"Hiçbir encoder çalışmadı {at}{notProbed}.";

        string Source(bool preferred) => preferred ? "PC tercihi" : "otomatik";
        var first = selections[0];
        bool same = selections.All(s => s.Selected?.Candidate.Kind == first.Selected?.Candidate.Kind);
        string text = same && first.Selected is { } sel
            ? $"Seçilen: {sel.Candidate.DisplayName} · {CodecText(sel.Candidate.Codec)} — {sel.Candidate.Chain} ({Source(first.IsPreferenceApplied)})"
            : "Seçilen: " + string.Join(", ", selections.Select(s =>
                $"Monitör {s.Index} → {(s.Selected is { } r ? $"{r.Candidate.DisplayName} ({Source(s.IsPreferenceApplied)})" : "yok")}"));

        var unavailable = selections.Where(s => s.PreferenceUnavailable).Select(s => s.Index).ToList();
        if (unavailable.Count > 0)
            text += $" — tercih edilen encoder Monitör {string.Join(", ", unavailable)}'de çalışmadı, orada otomatik seçim kullanılıyor";

        return $"{text} {at}{notProbed}";
    }

    private static string CodecText(VideoCodec c) => c switch { VideoCodec.H264 => "H.264", VideoCodec.Vp9 => "VP9", _ => c.ToString() };
    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
