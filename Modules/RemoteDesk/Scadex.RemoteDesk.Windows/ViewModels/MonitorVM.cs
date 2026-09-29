using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary> Sınama tablosunun bir satırı. </summary>
public sealed class ProbeRowVM
{
    public required string Symbol { get; init; }
    public required Brush SymbolBrush { get; init; }
    public required string Encoder { get; init; }
    public required string Chain { get; init; }
    public required string Detail { get; init; }
    public required bool IsSelected { get; init; }
    /// <summary> Yalnızca çalışan adaylar elle denenebilir. </summary>
    public required bool CanTry { get; init; }
    public required ICommand TryCommand { get; init; }
}

/// <summary>
/// Tek monitörün kartı: donanım bilgisi, kodlayıcı sınaması ve test yayınının canlı durumu. Servis olayları iş
/// parçacığı havuzundan gelir; <see cref="HomeVM"/> onları Dispatcher'a taşıyıp <see cref="Apply"/>'ı çağırır.
/// </summary>
public sealed class MonitorVM : BaseViewModel
{
    private static readonly Brush Ok = Freeze(Color.FromRgb(0x2E, 0x9E, 0x5B));
    private static readonly Brush Bad = Freeze(Color.FromRgb(0xD1, 0x43, 0x43));
    private static readonly Brush Warn = Freeze(Color.FromRgb(0xC7, 0x8A, 0x10));
    private static readonly Brush Muted = Freeze(Color.FromRgb(0x80, 0x88, 0x94));
    private static readonly Brush Info = Freeze(Color.FromRgb(0x2F, 0x6F, 0xC9));

    private readonly IEncoderProbeService _probes;
    private readonly IScreenStreamService _streams;
    private readonly Func<StreamTarget> _target;

    public MonitorVM(MonitorInfo monitor, IEncoderProbeService probes, IScreenStreamService streams, Func<StreamTarget> target)
    {
        Monitor = monitor;
        _probes = probes;
        _streams = streams;
        _target = target;

        ProbeCommand = new RelayCommand(async _ => await ProbeAsync(), _ => !IsBusy);
        StartCommand = new RelayCommand(_ => _streams.Start(Monitor, _target()), _ => !IsActive);
        StopCommand = new RelayCommand(async _ => await _streams.StopAsync(Monitor.Index), _ => IsActive);

        if (_probes.GetCached(monitor.Index) is { } cached) ShowProbe(cached);
        Apply(_streams.GetStatus(monitor.Index));
    }

    public MonitorInfo Monitor { get; }

    public string Title => $"Monitör {Monitor.Index}{(Monitor.IsPrimary ? " (birincil)" : "")} — {Monitor.DeviceName.Replace(@"\\.\", "")}";
    public string Geometry => $"{Monitor.Width}×{Monitor.Height} · konum ({Monitor.Left}, {Monitor.Top})";
    public string Gpu => $"{Monitor.GpuName} · {VendorText(Monitor.GpuVendor)} · adaptör {Monitor.AdapterIndex}, çıkış {Monitor.OutputIndex}";

    public ICommand ProbeCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    // ---------------------------------------------------------------- sınama

    public ObservableCollection<ProbeRowVM> ProbeRows { get; } = [];

    private string _probeSummary = "Henüz sınanmadı — \"Kodlayıcıları sına\" ya da test yayını başlatınca sınanır.";
    public string ProbeSummary { get => _probeSummary; private set => SetProperty(ref _probeSummary, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommands(); } }

    private async Task ProbeAsync()
    {
        IsBusy = true;
        ProbeSummary = "Sınanıyor… her aday birkaç kare kodluyor.";
        try
        {
            ShowProbe(await _probes.ProbeAsync(Monitor, CancellationToken.None));
        }
        catch (Exception ex)
        {
            ProbeSummary = $"Sınama yapılamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary> Sahada karşılaştırma: otomatik seçimi değiştirmeden bu adayla yayını yeniden başlatır. </summary>
    private async Task TryAsync(EncoderCandidate candidate)
    {
        await _streams.StopAsync(Monitor.Index);
        _streams.Start(Monitor, _target(), candidate);
    }

    private void ShowProbe(MonitorProbe probe)
    {
        ProbeRows.Clear();
        ProbeResult? selected = probe.Selected;
        foreach (ProbeResult r in probe.Results)
        {
            ProbeRows.Add(new ProbeRowVM
            {
                Symbol = r.Ok ? "✓" : "✗",
                SymbolBrush = r.Ok ? Ok : Bad,
                Encoder = $"{r.Candidate.DisplayName} · {CodecText(r.Candidate.Codec)}",
                Chain = r.Candidate.Chain,
                Detail = r.Ok ? $"çalışıyor ({r.Duration.TotalSeconds:0.0} sn)" : r.Reason ?? "",
                IsSelected = ReferenceEquals(r, selected),
                CanTry = r.Ok,
                TryCommand = new RelayCommand(async _ => await TryAsync(r.Candidate)),
            });
        }
        ProbeSummary = selected is null
            ? $"Hiçbir kodlayıcı çalışmadı ({probe.ProbedAt:HH:mm:ss})."
            : $"Seçilen: {selected.Candidate.DisplayName} · {CodecText(selected.Candidate.Codec)} — {selected.Candidate.Chain} ({probe.ProbedAt:HH:mm:ss})";
    }

    // ---------------------------------------------------------------- yayın durumu

    private bool _isActive;
    public bool IsActive { get => _isActive; private set { if (SetProperty(ref _isActive, value)) RaiseCommands(); } }

    private string _stateText = "";
    public string StateText { get => _stateText; private set => SetProperty(ref _stateText, value); }

    private Brush _stateBrush = Muted;
    public Brush StateBrush { get => _stateBrush; private set => SetProperty(ref _stateBrush, value); }

    private string _encoderText = "—";
    public string EncoderText { get => _encoderText; private set => SetProperty(ref _encoderText, value); }

    private string _profileText = "—";
    public string ProfileText { get => _profileText; private set => SetProperty(ref _profileText, value); }

    private string _liveText = "—";
    public string LiveText { get => _liveText; private set => SetProperty(ref _liveText, value); }

    private Brush _liveBrush = Muted;
    public Brush LiveBrush { get => _liveBrush; private set => SetProperty(ref _liveBrush, value); }

    private string _targetText = "—";
    public string TargetText { get => _targetText; private set => SetProperty(ref _targetText, value); }

    private string _messageText = "";
    public string MessageText { get => _messageText; private set => SetProperty(ref _messageText, value); }

    public void Apply(StreamStatus s)
    {
        IsActive = s.State is not (StreamState.Idle or StreamState.Failed);
        (StateText, StateBrush) = s.State switch
        {
            StreamState.Idle => ("Yayın yok", Muted),
            StreamState.Probing => ("Kodlayıcılar sınanıyor…", Info),
            StreamState.Starting => ("Başlıyor…", Info),
            StreamState.Streaming => ("● Yayında", Ok),
            StreamState.ScreenLocked => ("Ekran kilitli — kilit açılınca sürecek", Warn),
            StreamState.Retrying => ("Yeniden deneniyor", Warn),
            StreamState.Failed => ("Başarısız", Bad),
            _ => (s.State.ToString(), Muted),
        };

        if (s.Encoder is { } e)
            EncoderText = $"{e.DisplayName} · {CodecText(e.Codec)} · {e.Chain} · {(e.IsHardware ? "donanım" : "yazılım")} (ffmpeg: {e.FfmpegEncoder})" +
                          (s.IsForced ? " — ELLE SEÇİLDİ (otomatik seçim değil)" : " — otomatik seçim");
        if (s.Profile is { } p)
            ProfileText = $"{p}{(s.Encoder is { CanScale: false } ? " (bu zincir ölçeklemez; kaynak çözünürlük)" : "")}" +
                          (s.Downgrades > 0 ? $" — yetişemediği için {s.Downgrades} kez düşürüldü" : "");
        if (s.Target is not null) TargetText = s.Target;

        if (s.State == StreamState.Streaming)
        {
            // FFmpeg -progress, RTSP ve null çıkışında bit hızını "N/A" verir; hedef bit hızı profilde görünür.
            string bitrate = s.Bitrate is { } b && b != "N/A" ? $" · {b}" : "";
            LiveText = $"{s.Fps:0.#} fps · hız {s.Speed:0.00}x{bitrate} · CPU %{s.CpuPercent:0.#} · {s.MemoryMb} MB" +
                       (s.Restarts > 0 ? $" · {s.Restarts} yeniden başlatma" : "") +
                       (s.StartedAt is { } at ? $" · {DateTime.Now - at:hh\\:mm\\:ss}" : "");
            LiveBrush = s.Speed is < 0.9 ? Warn : Ok;
        }
        else if (!IsActive)
        {
            LiveText = "—";
            LiveBrush = Muted;
        }
        MessageText = s.Message ?? "";

        // İlk yayında yapılan sınama da tabloya yansısın.
        if (s.State is StreamState.Starting && ProbeRows.Count == 0 && _probes.GetCached(Monitor.Index) is { } probe) ShowProbe(probe);
    }

    private void RaiseCommands()
    {
        foreach (var c in new[] { ProbeCommand, StartCommand, StopCommand }) ((RelayCommand)c).RaiseCanExecuteChanged();
    }

    private static string CodecText(VideoCodec c) => c switch { VideoCodec.H264 => "H.264", VideoCodec.Vp9 => "VP9", _ => c.ToString() };
    private static string VendorText(GpuVendor v) => v switch
    {
        GpuVendor.Nvidia => "NVIDIA", GpuVendor.Intel => "Intel", GpuVendor.Amd => "AMD", _ => "bilinmeyen üretici",
    };
    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
