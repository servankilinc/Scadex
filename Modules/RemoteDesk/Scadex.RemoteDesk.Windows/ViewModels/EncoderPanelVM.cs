using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Agent;

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
/// "Encoder" kutucuğu — monitörlerden ayrı: sınama (tüm monitörler), aday listesi ve PC başına tercih (Agent'ın belleğinde). Sınama ve tercih
/// Agent'a pipe komutu olarak gider (arayüzün yapabildiği üç işten ikisi); sonuç Agent'ın özetiyle geri gelir (<see cref="Apply"/>).
/// Sınama monitör başına yapılır; liste adayları birleştirir ve bir aday bazı monitörlerde çalışmıyorsa bunu satırda söyler.
/// </summary>
public sealed class EncoderPanelVM : BaseViewModel
{
    private static readonly Brush Ok = Freeze(Color.FromRgb(0x2E, 0x9E, 0x5B));
    private static readonly Brush Bad = Freeze(Color.FromRgb(0xD1, 0x43, 0x43));
    private static readonly Brush Warn = Freeze(Color.FromRgb(0xC7, 0x8A, 0x10));

    private readonly IAgentLink _agent;
    private EncoderPanelView? _view;

    public EncoderPanelVM(IAgentLink agent)
    {
        _agent = agent;
        ProbeCommand = new RelayCommand(_ => _agent.ProbeEncoders(), _ => _view is { IsProbing: false } && _agent.State == AgentLinkState.Connected);
        ResetPreferenceCommand = new RelayCommand(_ => _agent.SetPreferredEncoder(null), _ => _view?.Preferred is not null);
    }

    public ICommand ProbeCommand { get; }
    public ICommand ResetPreferenceCommand { get; }

    public ObservableCollection<EncoderRowVM> Rows { get; } = [];

    private string _summary = "";
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public bool HasPreference => _view?.Preferred is not null;

    public string PreferenceText => _view?.Preferred is not null
        ? $"Tercih: {_view.PreferredName} — bu PC'deki tüm yayınlar bunu kullanır; çalışmadığı monitörde otomatik seçim. " +
          "Agent yeniden başlayınca otomatiğe döner."
        : "Tercih: otomatik (öncelik sırası). Değiştirmek için listede \"Tercih et\".";

    /// <summary> Agent'ın özetiyle (UI iş parçacığında). <paramref name="view"/> <c>null</c> = Agent'a bağlı değil. </summary>
    public void Apply(EncoderPanelView? view, IReadOnlyList<MonitorInfo> monitors)
    {
        _view = view;
        OnPropertyChanged(nameof(HasPreference));
        OnPropertyChanged(nameof(PreferenceText));
        ((RelayCommand)ProbeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ResetPreferenceCommand).RaiseCanExecuteChanged();

        Rows.Clear();
        if (view is null)
        {
            Summary = "Agent'a bağlı değil.";
            return;
        }
        if (view.IsProbing)
        {
            Summary = $"Sınanıyor… ({monitors.Count} monitör) — her aday birkaç kare kodluyor.";
            return;
        }
        if (view.ProbeError is { } error)
        {
            Summary = $"Sınama yapılamadı: {error}";
            return;
        }
        ShowProbes(view, monitors.Count);
    }

    private void ShowProbes(EncoderPanelView view, int monitorCount)
    {
        var probes = view.Probes;
        if (probes.Count == 0)
        {
            Summary = "Henüz sınanmadı — \"Encoder'ları sına\" ya da ilk izlemede kendiliğinden sınanır.";
            return;
        }

        // Sıra: birincil monitörün öncelik sırası (yoksa ilk sınanan); diğer monitörlerde olup onda olmayan adaylar sona.
        var primary = probes.FirstOrDefault(p => p.Monitor.IsPrimary) ?? probes[0];
        var candidates = primary.Results.Select(r => r.Encoder)
            .Concat(probes.SelectMany(p => p.Results.Select(r => r.Encoder)))
            .DistinctBy(c => c.Kind)
            .ToList();

        foreach (var candidate in candidates)
        {
            var results = probes.Select(p => (p.Monitor, Result: p.Results.FirstOrDefault(r => r.Encoder.Kind == candidate.Kind)))
                .Where(x => x.Result is not null)
                .ToList();
            var working = results.Where(x => x.Result!.Ok).ToList();
            var failing = results.Where(x => !x.Result!.Ok).ToList();

            bool isSelected = probes.All(p => p.SelectedKind == candidate.Kind);
            (string symbol, Brush brush) = (working.Count, failing.Count) switch
            {
                (> 0, 0) => ("✓", Ok),
                (0, _) => ("✗", Bad),
                _ => ("◐", Warn),
            };
            string detail = failing.Count == 0
                ? $"çalışıyor ({working.Max(x => x.Result!.DurationSeconds):0.0} sn)"
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
                PreferCommand = new RelayCommand(_ => _agent.SetPreferredEncoder(kind)),
            });
        }

        Summary = BuildSummary(probes, monitorCount);
    }

    private static string BuildSummary(IReadOnlyList<MonitorProbeView> probes, int monitorCount)
    {
        string at = $"({probes.Max(p => p.ProbedAt):HH:mm:ss})";
        string notProbed = probes.Count < monitorCount ? $" · {monitorCount - probes.Count} monitör henüz sınanmadı" : "";

        EncoderView? Selected(MonitorProbeView p) => p.Results.FirstOrDefault(r => r.Encoder.Kind == p.SelectedKind)?.Encoder;
        if (probes.All(p => p.SelectedKind is null))
            return $"Hiçbir encoder çalışmadı {at}{notProbed}.";

        string Source(bool preferred) => preferred ? "PC tercihi" : "otomatik";
        var first = probes[0];
        bool same = probes.All(p => p.SelectedKind == first.SelectedKind);
        string text = same && Selected(first) is { } sel
            ? $"Seçilen: {sel.DisplayName} · {CodecText(sel.Codec)} — {sel.Chain} ({Source(first.IsPreferenceApplied)})"
            : "Seçilen: " + string.Join(", ", probes.Select(p =>
                $"Monitör {p.Monitor.Index} → {(Selected(p) is { } r ? $"{r.DisplayName} ({Source(p.IsPreferenceApplied)})" : "yok")}"));

        var unavailable = probes.Where(p => p.PreferenceUnavailable).Select(p => p.Monitor.Index).ToList();
        if (unavailable.Count > 0)
            text += $" — tercih edilen encoder Monitör {string.Join(", ", unavailable)}'de çalışmadı, orada otomatik seçim kullanılıyor";

        return $"{text} {at}{notProbed}";
    }

    private static string CodecText(VideoCodec c) => c switch { VideoCodec.H264 => "H.264", VideoCodec.Vp9 => "VP9", _ => c.ToString() };
    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
