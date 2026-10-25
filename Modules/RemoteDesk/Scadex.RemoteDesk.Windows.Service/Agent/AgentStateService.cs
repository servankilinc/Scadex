using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Service.Services.Connection;
using Scadex.RemoteDesk.Windows.Service.Services.Encoding;
using Scadex.RemoteDesk.Windows.Service.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Service.Services.FileTransfer;
using Scadex.RemoteDesk.Windows.Service.Services.Input;
using Scadex.RemoteDesk.Windows.Service.Services.Monitors;
using Scadex.RemoteDesk.Windows.Service.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.Service.Agent;

/// <summary>
/// Agent servislerinin durumunu arayüzün göstereceği tek bir <see cref="AgentSnapshot"/>'a çevirir ve arayüzün üç zararsız komutunu
/// (yeniden bağlan, encoder sına, tercih et) uygular. Eskiden WPF ViewModel'lerinin servislerden doğrudan okuduğu şeyler burada toplanır.
/// <para/>
/// <b>Boşta iş yok:</b> arayüz bağlı değilken (<see cref="Subscribers"/> = 0) özet kurulmaz ve <see cref="Changed"/> tetiklenmez.
/// Bağlıyken değişiklikler en çok <see cref="Coalesce"/>'da bir birleştirilir (yayın ilerlemesi saniyede bir gelir).
/// </summary>
public sealed class AgentStateService : IDisposable
{
    private static readonly TimeSpan Coalesce = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MonitorCacheAge = TimeSpan.FromSeconds(5);

    private readonly ICentralConnection _connection;
    private readonly IScreenStreamService _streams;
    private readonly IRemoteInputService _input;
    private readonly IEncoderProbeService _probes;
    private readonly IMonitorService _monitors;
    private readonly IFfmpegLocator _ffmpeg;
    private readonly SessionContext _session;
    private readonly ILogger<AgentStateService> _logger;

    private readonly Lock _gate = new();
    private IReadOnlyList<MonitorInfo> _monitorCache = [];
    private DateTime _monitorsReadUtc = DateTime.MinValue;
    private int _subscribers;
    private int _pending;
    private int _activeTransfers;
    private volatile bool _probing;
    private string? _probeError;

    public AgentStateService(ICentralConnection connection, IScreenStreamService streams, IRemoteInputService input, IFileTransferService files,
        IEncoderProbeService probes, IMonitorService monitors, IFfmpegLocator ffmpeg, SessionContext session, ILogger<AgentStateService> logger)
    {
        _connection = connection;
        _streams = streams;
        _input = input;
        _probes = probes;
        _monitors = monitors;
        _ffmpeg = ffmpeg;
        _session = session;
        _logger = logger;

        _connection.StatusChanged += _ => MarkDirty();
        _streams.StatusChanged += _ => MarkDirty();
        _input.ControllerChanged += _ => MarkDirty();
        _probes.PreferenceChanged += MarkDirty;
        _probes.Probed += MarkDirty;
        files.ActiveChanged += active =>
        {
            Volatile.Write(ref _activeTransfers, active ? 1 : 0);
            MarkDirty();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary> Özet değişti (birleştirilmiş, iş parçacığı havuzundan). Yalnızca abone varken tetiklenir. </summary>
    public event Action? Changed;

    /// <summary> Bağlı arayüz sayısı; pipe sunucusu yönetir. </summary>
    public int Subscribers => Volatile.Read(ref _subscribers);

    public void AddSubscriber() => Interlocked.Increment(ref _subscribers);
    public void RemoveSubscriber() => Interlocked.Decrement(ref _subscribers);

    public static string AgentVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";

    public AgentSnapshot Build()
    {
        var monitors = ReadMonitors();
        var status = _connection.Status;
        FfmpegInfo ffmpeg = _ffmpeg.Info;
        EncoderKind? preferred = _probes.Preferred;

        return new AgentSnapshot(
            AgentVersion,
            Environment.MachineName,
            _session.UserName,
            _connection.DeviceId,
            new AgentConnectionView(ToView(status.State), status.CentralUrl, status.Detail, status.DeviceName, status.CabinetName),
            new FfmpegView(ffmpeg.Path, ffmpeg.Exists, ffmpeg.Version, ffmpeg.IsGpl),
            [.. monitors.Select(m => new AgentMonitorView(m, ToView(_streams.GetStatus(m.Index))))],
            new EncoderPanelView(
                _probing,
                preferred,
                preferred is { } kind ? EncoderCatalog.Find(kind).DisplayName : null,
                [.. monitors.Select(m => _probes.GetCached(m.Index)).OfType<MonitorProbe>().Select(ToView)],
                _probeError),
            _input.Controller,
            Volatile.Read(ref _activeTransfers) > 0);
    }

    // ---- Arayüzün üç komutu (AgentPipeContract) — başka komut yoktur ----

    public void Reconnect()
    {
        _logger.LogInformation("Arayüzden: yeniden bağlan");
        _connection.Reconnect();
    }

    public void SetPreferred(EncoderKind? kind)
    {
        // Bilinmeyen bir sayı enum'a sorunsuz çözülür; katalogda olmayan aday tercih edilemez.
        if (kind is { } k && !Enum.IsDefined(k)) return;
        _logger.LogInformation("Arayüzden: encoder tercihi {Preferred}", kind?.ToString() ?? "otomatik");
        _probes.SetPreferred(kind);
    }

    /// <summary> Tüm monitörleri sırayla sınar (aynı anda tek sınama). Zaten sınanıyorsa yok sayılır. </summary>
    public async Task ProbeAllAsync()
    {
        if (_probing) return;
        _probing = true;
        _probeError = null;
        MarkDirty();
        try
        {
            _logger.LogInformation("Arayüzden: encoder'ları sına");
            foreach (var monitor in _monitors.GetMonitors())
                await _probes.ProbeAsync(monitor, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Encoder sınaması yapılamadı");
            _probeError = _ffmpeg.Info.Exists ? "Sınama yapılamadı; ayrıntı Agent günlüğünde." : "FFmpeg bulunamadı.";
        }
        finally
        {
            _probing = false;
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        if (Subscribers == 0 || Interlocked.Exchange(ref _pending, 1) == 1) return;
        _ = Task.Delay(Coalesce).ContinueWith(_ =>
        {
            Volatile.Write(ref _pending, 0);
            try { Changed?.Invoke(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Arayüz özeti gönderilemedi"); }
        }, TaskScheduler.Default);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_gate) _monitorsReadUtc = DateTime.MinValue;
        MarkDirty();
    }

    /// <summary> DXGI listesi ucuzdur ama yayın sırasında saniyede bir özet kurulur: 5 sn önbellek + ekran değişince tazeleme. </summary>
    private IReadOnlyList<MonitorInfo> ReadMonitors()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _monitorsReadUtc < MonitorCacheAge) return _monitorCache;
            try { _monitorCache = _monitors.GetMonitors(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Monitörler okunamadı"); _monitorCache = []; }
            _monitorsReadUtc = DateTime.UtcNow;
            return _monitorCache;
        }
    }

    private static AgentConnectionState ToView(ConnectionState state) => state switch
    {
        ConnectionState.NotConfigured => AgentConnectionState.NotConfigured,
        ConnectionState.Connecting => AgentConnectionState.Connecting,
        ConnectionState.Connected => AgentConnectionState.Connected,
        ConnectionState.Unauthorized => AgentConnectionState.Unauthorized,
        ConnectionState.AlreadyConnected => AgentConnectionState.AlreadyConnected,
        ConnectionState.NetworkNotAllowed => AgentConnectionState.NetworkNotAllowed,
        _ => AgentConnectionState.Disconnected,
    };

    private static StreamView ToView(StreamStatus s) => new(
        (AgentStreamState)(int)s.State, s.SessionId, s.Encoder is { } e ? ToView(e) : null, s.IsForced, s.Profile, s.Target, s.StartedAt,
        s.Fps, s.Speed, s.Bitrate, s.CpuPercent, s.MemoryMb, s.Downgrades, s.Restarts, s.Message);

    private static EncoderView ToView(EncoderCandidate c) => new(c.Kind, c.DisplayName, c.Codec, c.Chain, c.IsHardware, c.FfmpegEncoder, c.CanScale);

    private static MonitorProbeView ToView(MonitorProbe p) => new(
        p.Monitor, p.ProbedAt,
        [.. p.Results.Select(r => new ProbeResultView(ToView(r.Candidate), r.Ok, r.Reason, r.Duration.TotalSeconds))],
        p.Selected?.Candidate.Kind, p.IsPreferenceApplied, p.PreferenceUnavailable);

    public void Dispose() => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
}
