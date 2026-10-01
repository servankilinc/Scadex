using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Hubs;
using Scadex.RemoteDesk.Model.Dtos.Control;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Streaming;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Control;

/// <summary>
/// Uzaktan kontrolün canlı durumu — SINGLETON, bellekte (RemoteDesk.md § 12). Tek sunucu varsayımı.
/// <list type="bullet">
/// <item>PC başına aynı anda <b>tek</b> kontrol eden kullanıcı; diğerleri izlemeye devam eder. Aynı kullanıcının başka sekmesi kontrolü devralır.</item>
/// <item>Kontrol yalnızca o PC'yi İZLEYENE verilir (canlı kiralama); izleme biterse kontrol de biter — görmeden tıklanmaz.</item>
/// <item>Girdi sunucuda saklanmaz, sıraya alınmaz: doğrulanıp PC'ye hemen iletilir. Tarayıcının bağlantısı başına hub çağrıları sıralı
/// işlendiği için (SignalR varsayılanı) olay sırası korunur.</item>
/// <item>Faz 8 yalnızca fare: klavye olayları (Faz 9) iletilmez.</item>
/// </list>
/// </summary>
public sealed class RemoteControlCoordinator
{
    /// <summary> Bu kadar süre girdi gelmezse kontrol düşer (kontrolü alıp unutan kullanıcı PC'yi başkalarına kilitlemesin). </summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary> Paket başına üst sınır: ~60 Hz paketlerde birkaç olay beklenir; fazlası bozuk/kötü niyetli istemcidir. </summary>
    private const int MaxEventsPerBatch = 256;

    /// <summary> Tekerlek adımı sınırı (Windows birimi; 120 = bir çentik). </summary>
    private const int MaxWheelDelta = 120 * 20;

    private readonly PcConnectionRegistry _connections;
    private readonly ScreenStreamCoordinator _streams;
    private readonly IHubContext<PcHub, IPcHubClient> _pcHub;
    private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub;
    private readonly RemoteControlStore _store;
    private readonly ILogger<RemoteControlCoordinator> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<Guid, ControlSession> _byDevice = [];

    public RemoteControlCoordinator(PcConnectionRegistry connections, ScreenStreamCoordinator streams, IHubContext<PcHub, IPcHubClient> pcHub,
        IHubContext<ViewerHub, IViewerHubClient> viewerHub, RemoteControlStore store, ILogger<RemoteControlCoordinator> logger)
    {
        _connections = connections;
        _streams = streams;
        _pcHub = pcHub;
        _viewerHub = viewerHub;
        _store = store;
        _logger = logger;
    }

    /// <summary> Kontrol ister. Aynı bağlantıdan tekrar istek aynı oturumu döndürür (idempotent). </summary>
    public async Task<RequestControlResponse> RequestAsync(Guid deviceId, Guid userId, string userName, string viewerConnectionId, CancellationToken cancellationToken)
    {
        if (!_connections.TryGet(deviceId, out var pc))
            return Denied(ControlRequestStatus.PcNotConnected, "PC merkeze bağlı değil.");
        if (!await _streams.IsViewingAsync(deviceId, userId, cancellationToken))
            return Denied(ControlRequestStatus.NotViewing, "Kontrol için önce PC'nin ekranını izlemelisiniz.");

        ControlSession session;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_byDevice.TryGetValue(deviceId, out var current))
            {
                if (current.ViewerConnectionId == viewerConnectionId)
                    return Granted(current);

                if (current.UserId != userId)
                    return new RequestControlResponse
                    {
                        Status = ControlRequestStatus.Busy,
                        ControllerName = current.UserName,
                        Message = $"PC'yi şu an {current.UserName} kontrol ediyor.",
                        IdleTimeoutSec = (int)IdleTimeout.TotalSeconds
                    };

                await EndCoreAsync(current, RemoteControlEndReason.Replaced, notifyPc: true, notifyViewer: true);
            }

            session = new ControlSession(Guid.NewGuid(), deviceId, userId, userName, viewerConnectionId, pc.ConnectionId, DateTime.UtcNow);
            await _store.StartAsync(session.Id, deviceId, userId, cancellationToken);
            _byDevice[deviceId] = session;

            // Girdiden ÖNCE gitmeli: yanıtı alan tarayıcı hemen paket gönderir; aynı PC bağlantısında sıra korunur.
            await _pcHub.Clients.Client(pc.ConnectionId).ControlStarted(new ControlStartedCommand(session.Id, userName));
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("RemoteDesk: {User} ({UserId}) {DeviceId} PC'sinin kontrolünü aldı (oturum {ControlSessionId})",
            userName, userId, deviceId, session.Id);
        return Granted(session);
    }

    /// <summary>
    /// Girdi paketini doğrular ve PC'ye iletir. Bu bağlantının oturumu değilse (bitmiş, başka sekme almış) sessizce yok sayılır —
    /// tarayıcı bitişi <c>ControlEnded</c> ile zaten öğrenir; paket başına hata üretmek 60 Hz'de gürültü olurdu.
    /// </summary>
    public async Task InputAsync(string viewerConnectionId, InputBatch batch)
    {
        string pcConnectionId;
        InputEvent[] events;

        await _lock.WaitAsync();
        try
        {
            var session = _byDevice.Values.FirstOrDefault(s => s.Id == batch.ControlSessionId);
            if (session is null || session.ViewerConnectionId != viewerConnectionId)
                return;
            if (!_connections.TryGet(session.DeviceId, out var pc) || pc.ConnectionId != session.PcConnectionId
                || pc.Monitors.All(m => m.Index != batch.MonitorIndex))
                return;

            events = Sanitize(batch.Events);
            if (events.Length == 0)
                return;

            session.LastInputUtc = DateTime.UtcNow;
            session.InputEventCount += events.Length;
            pcConnectionId = session.PcConnectionId;
        }
        finally
        {
            _lock.Release();
        }

        await _pcHub.Clients.Client(pcConnectionId).Input(batch with { Events = events });
    }

    /// <summary> Kullanıcı bıraktı. Başka bağlantının oturumunu bırakamaz. </summary>
    public async Task ReleaseAsync(string viewerConnectionId, Guid controlSessionId)
    {
        await _lock.WaitAsync();
        try
        {
            var session = _byDevice.Values.FirstOrDefault(s => s.Id == controlSessionId && s.ViewerConnectionId == viewerConnectionId);
            if (session is not null)
                await EndCoreAsync(session, RemoteControlEndReason.Released, notifyPc: true, notifyViewer: false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task OnViewerDisconnectedAsync(string viewerConnectionId)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var session in _byDevice.Values.Where(s => s.ViewerConnectionId == viewerConnectionId).ToList())
                await EndCoreAsync(session, RemoteControlEndReason.ViewerDisconnected, notifyPc: true, notifyViewer: false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> PC koptu: istemci kopunca basılı düğmeleri zaten kendisi bırakır (§ 12.5); yalnızca tarayıcıya bildirilir. </summary>
    public async Task OnPcDisconnectedAsync(Guid deviceId, string pcConnectionId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_byDevice.TryGetValue(deviceId, out var session) && session.PcConnectionId == pcConnectionId)
                await EndCoreAsync(session, RemoteControlEndReason.PcDisconnected, notifyPc: false, notifyViewer: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> PC ekranı için: şu an kimin kontrol ettiği. </summary>
    public async Task<PcControlDto?> GetControllerAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _byDevice.TryGetValue(deviceId, out var s)
                ? new PcControlDto { UserId = s.UserId, UserName = s.UserName, StartedUtc = s.StartedUtc }
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> Boşta kalan kontroller ve izlemesi biten kullanıcılar (ScreenStreamWorker, 5 sn'de bir). Kontrol yokken iş yapmaz. </summary>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        List<ControlSession> sessions;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_byDevice.Count == 0)
                return;
            sessions = [.. _byDevice.Values];
        }
        finally
        {
            _lock.Release();
        }

        // Kiralama sorusu yayın koordinatörünün kilidini alır: kendi kilidimizi tutarken sorulmaz.
        var ended = new List<(ControlSession Session, RemoteControlEndReason Reason)>();
        var now = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            if (now - session.LastInputUtc > IdleTimeout)
                ended.Add((session, RemoteControlEndReason.Idle));
            else if (!await _streams.IsViewingAsync(session.DeviceId, session.UserId, cancellationToken))
                ended.Add((session, RemoteControlEndReason.ViewEnded));
        }
        if (ended.Count == 0)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (var (session, reason) in ended)
            {
                // Arada bırakılmış ya da yenisiyle değişmiş olabilir.
                if (_byDevice.TryGetValue(session.DeviceId, out var current) && current == session)
                    await EndCoreAsync(session, reason, notifyPc: true, notifyViewer: true);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> Kilit altında çağrılır. </summary>
    private async Task EndCoreAsync(ControlSession session, RemoteControlEndReason reason, bool notifyPc, bool notifyViewer)
    {
        _byDevice.Remove(session.DeviceId);

        try
        {
            await _store.EndAsync(session.Id, reason, session.InputEventCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoteDesk: kontrol oturumu kaydı kapatılamadı {ControlSessionId}", session.Id);
        }

        if (notifyPc)
            await _pcHub.Clients.Client(session.PcConnectionId).ControlEnded(new ControlEndedCommand(session.Id));
        if (notifyViewer)
            await _viewerHub.Clients.Client(session.ViewerConnectionId).ControlEnded(new ControlEndedNotice { ControlSessionId = session.Id, Reason = reason });

        _logger.LogInformation("RemoteDesk: {User} {DeviceId} kontrolü bitti ({Reason}, {Count} olay, {Duration:0} sn)",
            session.UserName, session.DeviceId, reason, session.InputEventCount, (DateTime.UtcNow - session.StartedUtc).TotalSeconds);
    }

    /// <summary> Yalnızca fare olayları (Faz 8), aralık dışı değerler kırpılır, zorunlu alanı eksik olay atılır. </summary>
    private static InputEvent[] Sanitize(InputEvent[]? events)
    {
        if (events is null || events.Length == 0)
            return [];

        var result = new List<InputEvent>(Math.Min(events.Length, MaxEventsPerBatch));
        foreach (var e in events.Take(MaxEventsPerBatch))
        {
            switch (e.Type)
            {
                case InputEventType.Move when e.X is { } x && e.Y is { } y:
                    result.Add(e with { X = Clamp01(x), Y = Clamp01(y), Button = null, DeltaX = null, DeltaY = null, Code = null });
                    break;
                case InputEventType.Down or InputEventType.Up when e.Button is MouseButton.Left or MouseButton.Middle or MouseButton.Right:
                    bool hasPoint = e.X is not null && e.Y is not null;
                    result.Add(e with
                    {
                        X = hasPoint ? Clamp01(e.X!.Value) : null,
                        Y = hasPoint ? Clamp01(e.Y!.Value) : null,
                        DeltaX = null, DeltaY = null, Code = null
                    });
                    break;
                case InputEventType.Wheel when e.DeltaX is not null || e.DeltaY is not null:
                    result.Add(e with
                    {
                        X = null, Y = null, Button = null, Code = null,
                        DeltaX = e.DeltaX is { } dx ? Math.Clamp(dx, -MaxWheelDelta, MaxWheelDelta) : null,
                        DeltaY = e.DeltaY is { } dy ? Math.Clamp(dy, -MaxWheelDelta, MaxWheelDelta) : null
                    });
                    break;
                // KeyDown/KeyUp: Faz 9. Diğer/eksik olaylar atılır.
            }
        }
        return [.. result];
    }

    private static double Clamp01(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private static RequestControlResponse Granted(ControlSession s) => new()
    {
        Status = ControlRequestStatus.Granted,
        ControlSessionId = s.Id,
        IdleTimeoutSec = (int)IdleTimeout.TotalSeconds
    };

    private static RequestControlResponse Denied(ControlRequestStatus status, string message) => new()
    {
        Status = status,
        Message = message,
        IdleTimeoutSec = (int)IdleTimeout.TotalSeconds
    };

    private sealed class ControlSession(Guid id, Guid deviceId, Guid userId, string userName, string viewerConnectionId, string pcConnectionId, DateTime startedUtc)
    {
        public Guid Id { get; } = id;
        public Guid DeviceId { get; } = deviceId;
        public Guid UserId { get; } = userId;
        public string UserName { get; } = userName;
        public string ViewerConnectionId { get; } = viewerConnectionId;
        public string PcConnectionId { get; } = pcConnectionId;
        public DateTime StartedUtc { get; } = startedUtc;
        public DateTime LastInputUtc { get; set; } = startedUtc;
        public int InputEventCount { get; set; }
    }
}
