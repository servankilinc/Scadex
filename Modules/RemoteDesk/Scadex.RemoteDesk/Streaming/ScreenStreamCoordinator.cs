using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.Business.Settings;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Hubs;
using Scadex.RemoteDesk.Media;
using Scadex.RemoteDesk.Model.Dtos.View.Queries;
using Scadex.RemoteDesk.Realtime;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Streaming;

/// <summary>
/// PC ekran yayınlarının ve izleyici kiralamalarının canlı durumu — SINGLETON, bellekte.
/// <list type="bullet">
/// <item>(PC, monitör) başına en fazla bir yayın; aynı yayını istenen sayıda izleyici paylaşır (izleyici başına bir kiralama).</item>
/// <item>Yayın ilk izleyiciyle başlar; kiralaması kalmayınca <see cref="NoViewerGrace"/> sonra durur.</item>
/// <item>Durdurmada yayın bileti HEMEN silinir; istemci uymazsa <see cref="KickAfter"/> sonra yayıncı MediaMTX'ten atılır.</item>
/// </list>
/// MediaMTX'teki <c>dpc_</c> yolu yapılandırmada değildir; yayıncıyla yaşar ve yayıncı gidince kendiliğinden kalkar — temizlenecek yol yoktur.
/// Tek sunucu varsayımı (PcConnectionRegistry ile aynı). Kilit tek ve kısadır: bekleme (yol hazır mı) kilit dışında yapılır.
/// </summary>
public sealed class ScreenStreamCoordinator
{
    public static readonly TimeSpan LeaseRenewInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan NoViewerGrace = TimeSpan.FromSeconds(10);
    /// <summary> Yedek emniyet: kiralama var ama MediaMTX'te okuyucu yok (sekme açık, oynatıcı kopmuş). </summary>
    public static readonly TimeSpan NoReaderGrace = TimeSpan.FromSeconds(60);
    /// <summary>
    /// İlk yayında istemci kodlayıcıları sınar (monitör başına bir kez, sonra önbellekte); donanım adayları başarısız olup VP9'a inen makinede
    /// bu 10–20 sn sürebilir. Sonraki yayınlar 1–3 sn'de hazır olur.
    /// </summary>
    public static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan KickAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(300);

    private readonly PcConnectionRegistry _connections;
    private readonly IHubContext<PcHub, IPcHubClient> _hub;
    private readonly ScreenTicketStore _tickets;
    private readonly ScreenSessionStore _store;
    private readonly IServiceScopeFactory _scopes;
    private readonly RemoteDeskOptions _options;
    private readonly MediaGatewaySettings _mediaSettings;
    private readonly ILogger<ScreenStreamCoordinator> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<(Guid DeviceId, int Monitor), ScreenStream> _streams = [];
    private readonly Dictionary<Guid, ViewLease> _leases = [];

    public ScreenStreamCoordinator(PcConnectionRegistry connections, IHubContext<PcHub, IPcHubClient> hub, ScreenTicketStore tickets,
        ScreenSessionStore store, IServiceScopeFactory scopes, IOptions<RemoteDeskOptions> options, IOptions<MediaGatewaySettings> mediaSettings,
        ILogger<ScreenStreamCoordinator> logger)
    {
        _connections = connections;
        _hub = hub;
        _tickets = tickets;
        _store = store;
        _scopes = scopes;
        _options = options.Value;
        _mediaSettings = mediaSettings.Value;
        _logger = logger;
    }

    #region İzleyici (tarayıcı) tarafı
    /// <summary> İzlemeyi başlatır: yayın yoksa PC'ye komut gider ve yol MediaMTX'te hazır olana kadar beklenir (§ 8.2). </summary>
    public async Task<Result<ScreenViewDto>> StartViewAsync(Guid deviceId, int monitorIndex, Guid userId, CancellationToken cancellationToken)
    {
        if (!_connections.TryGet(deviceId, out var connection))
            return Result<ScreenViewDto>.Validation(Error("DeviceId", "PC bağlı değil: Windows istemcisi merkeze bağlı olmalı."));
        if (connection.Monitors.All(m => m.Index != monitorIndex))
            return Result<ScreenViewDto>.Validation(Error("MonitorIndex", "PC'de bu numarada monitör yok."));

        ScreenStream stream;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_streams.TryGetValue((deviceId, monitorIndex), out var existing))
            {
                // Durmakta olan ya da eski bağlantıya ait yayın yeni izleyiciye verilmez; istemci aynı monitöre gelen yeni oturumla eskisini bırakır.
                if (existing.IsStopping)
                    _streams.Remove((deviceId, monitorIndex));
                else if (existing.ConnectionId != connection.ConnectionId)
                    await FinishAsync(existing, ScreenSessionStatus.Failed, ScreenStopReason.ClientDisconnected, "PC yeniden bağlandı.", notifyClient: false);

                if (!_streams.ContainsKey((deviceId, monitorIndex)))
                    existing = null;
            }

            stream = existing ?? await OpenStreamAsync(deviceId, monitorIndex, connection.ConnectionId, cancellationToken);
            // Hazır olmayı bekleyen izleyici süpürmede izleyici sayılır: ilk yayında kodlayıcı sınaması uzarsa yayın beklerken durdurulmasın.
            stream.PendingStarts++;
        }
        finally
        {
            _lock.Release();
        }

        string? failure;
        try
        {
            failure = await WaitReadyAsync(stream, cancellationToken);
        }
        finally
        {
            await _lock.WaitAsync(CancellationToken.None);
            stream.PendingStarts--;
            if (stream.PendingStarts == 0 && _leases.Values.All(l => l.Stream != stream))
                stream.LastViewerLeftUtc = DateTime.UtcNow;   // bekleyen gitti, izleyici de yok: bekleme süresi şimdi başlar
            _lock.Release();
        }
        if (failure is not null)
            return Result<ScreenViewDto>.Failure(message: failure, description: failure);

        var lease = new ViewLease(Guid.NewGuid(), stream, userId, DateTime.UtcNow);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (stream.IsStopping || !_streams.ContainsValue(stream))
                return Result<ScreenViewDto>.Failure(message: "Yayın başlarken durduruldu, yeniden deneyin.");

            lease.ViewLogId = await _store.StartViewAsync(stream.SessionId, deviceId, monitorIndex, userId, cancellationToken);
            _leases[lease.ViewId] = lease;
            stream.LastViewerLeftUtc = null;
        }
        finally
        {
            _lock.Release();
        }

        var (token, expiresUtc) = await _tickets.IssueReadAsync(stream.MediaPath, cancellationToken);
        _logger.LogInformation("RemoteDesk: {UserId} izlemeye başladı {Path} (kiralama {ViewId})", userId, stream.MediaPath, lease.ViewId);

        return Result<ScreenViewDto>.Success(new ScreenViewDto
        {
            ViewId = lease.ViewId,
            WhepUrl = $"{_mediaSettings.WebRtcPublicBaseUrl.TrimEnd('/')}/{stream.MediaPath}/whep",
            Token = token,
            ExpirationUtc = expiresUtc,
            LeaseRenewSec = (int)LeaseRenewInterval.TotalSeconds
        });
    }

    /// <summary> Kiralamayı uzatır. Düşmüş/başkasının kiralaması → 404 (izleme sona erdi). </summary>
    public async Task<Result> RenewAsync(Guid viewId, Guid userId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_leases.TryGetValue(viewId, out var lease) || lease.UserId != userId)
                return Result.NotFound(message: "İzleme sona erdi.");

            lease.LastRenewUtc = DateTime.UtcNow;
            return Result.Success();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> İzleyici ekrandan çıktı. Bilinmeyen kiralama için de başarılıdır (idempotent). </summary>
    public async Task<Result> ReleaseAsync(Guid viewId, Guid userId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_leases.TryGetValue(viewId, out var lease) && lease.UserId == userId)
                await EndLeaseAsync(lease);
            return Result.Success();
        }
        finally
        {
            _lock.Release();
        }
    }
    #endregion

    #region PC (istemci) tarafı
    /// <summary> İstemcinin yayın durumu bildirimi. Eski/bilinmeyen oturum yok sayılır. </summary>
    public async Task OnStreamStateAsync(Guid deviceId, string connectionId, StreamStateReport report)
    {
        await _lock.WaitAsync();
        try
        {
            var stream = _streams.Values.FirstOrDefault(s => s.SessionId == report.SessionId && s.DeviceId == deviceId && s.ConnectionId == connectionId);
            if (stream is null)
                return;

            stream.LastState = report.State;
            // Neden istemcinin sabit metnidir (FailureExplainer; adres/bilet maskeli) — bekleyen izleyiciye aynen gösterilir.
            if (report.FailureReason is not null)
                stream.LastFailureReason = report.FailureReason;
            stream.Encoder = report.Encoder ?? stream.Encoder;

            switch (report.State)
            {
                case ScreenStreamState.Failed:
                    string reason = report.FailureReason ?? "PC yayını başlatamadı.";
                    stream.Ready.TrySetResult(reason);
                    await FinishAsync(stream, ScreenSessionStatus.Failed, ScreenStopReason.ClientFailed, reason, notifyClient: false);
                    break;
                case ScreenStreamState.Stopped:
                    // Merkez istemeden durdu (PC'de elle durduruldu). Merkezin kendi durdurmasında FinishAsync zaten erken döner.
                    stream.Ready.TrySetResult("Yayın PC'de durduruldu.");
                    await FinishAsync(stream, ScreenSessionStatus.Stopped, ScreenStopReason.StoppedOnPc, null, notifyClient: false);
                    break;
                case ScreenStreamState.ScreenLocked when !stream.Ready.Task.IsCompleted:
                    // Yayın kilit açılınca kendiliğinden sürer; bekleyen izleyiciye hemen söylenir.
                    stream.Ready.TrySetResult("PC'nin ekranı kilitli ya da güvenli masaüstünde (UAC); kilit açılınca yeniden deneyin.");
                    break;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary> Hub bağlantısı koptu: o bağlantının yayınları biter (istemci de kendi FFmpeg'lerini durdurur). </summary>
    public async Task OnClientDisconnectedAsync(Guid deviceId, string connectionId)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var stream in _streams.Values.Where(s => s.DeviceId == deviceId && s.ConnectionId == connectionId).ToList())
            {
                stream.Ready.TrySetResult("PC'nin bağlantısı koptu.");
                await FinishAsync(stream, ScreenSessionStatus.Failed, ScreenStopReason.ClientDisconnected, "PC'nin bağlantısı koptu.", notifyClient: false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    #endregion

    #region Liste ekranı için okuma
    public async Task<IReadOnlyList<(int Monitor, ScreenStreamState? State, string? Encoder, int Viewers)>> SnapshotAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return [.. _streams.Values.Where(s => s.DeviceId == deviceId && !s.IsStopping)
                .Select(s => (s.MonitorIndex, s.LastState, s.Encoder, _leases.Values.Count(l => l.Stream == s)))];
        }
        finally
        {
            _lock.Release();
        }
    }
    #endregion

    #region Zamanlayıcı (ScreenStreamWorker)
    /// <summary> Düşen kiralamalar, izleyicisiz yayınlar, okuyucusuz yollar ve durdurmaya uymayan yayıncılar. Yayın yokken iş yapmaz. </summary>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_streams.Count == 0 && _leases.Count == 0)
                return;

            var now = DateTime.UtcNow;

            foreach (var lease in _leases.Values.Where(l => now - l.LastRenewUtc > LeaseTimeout).ToList())
            {
                _logger.LogInformation("RemoteDesk: kiralama yenilenmedi, düştü {ViewId} {Path}", lease.ViewId, lease.Stream.MediaPath);
                await EndLeaseAsync(lease);
            }

            foreach (var stream in _streams.Values.ToList())
            {
                if (stream.IsStopping)
                {
                    if (now - stream.StopRequestedUtc >= KickAfter)
                    {
                        // Bilet çoktan silindi: yayıncı hâlâ oradaysa istemci komuta uymamıştır → at (geri bağlanamaz).
                        var kick = await WithGatewayAsync(g => g.KickPublisherAsync(stream.MediaPath, cancellationToken));
                        if (!kick.IsSuccess)
                            _logger.LogWarning("RemoteDesk: yayıncı atılamadı {Path}: {Reason}", stream.MediaPath, kick.Error.Description);
                        _streams.Remove((stream.DeviceId, stream.MonitorIndex));
                    }
                    continue;
                }

                bool hasViewers = stream.PendingStarts > 0 || _leases.Values.Any(l => l.Stream == stream);
                if (!hasViewers)
                {
                    stream.LastViewerLeftUtc ??= now;
                    if (now - stream.LastViewerLeftUtc >= NoViewerGrace)
                        await FinishAsync(stream, ScreenSessionStatus.Stopped, ScreenStopReason.NoViewers, null, notifyClient: true);
                    continue;
                }
                // İzleyicisi var: hazırlık sırasında (kiralama yokken) konmuş damga kalırsa son izleyici çıkınca bekleme süresi atlanırdı.
                stream.LastViewerLeftUtc = null;

                if (stream.IsReady)
                {
                    var path = await WithGatewayAsync(g => g.GetRuntimePathAsync(stream.MediaPath, cancellationToken));
                    if (!path.IsSuccess)
                        continue;   // MediaMTX'e ulaşılamıyorsa karar verilmez

                    if (path.Data is { ReaderCount: > 0 })
                        stream.NoReadersSinceUtc = null;
                    else if ((stream.NoReadersSinceUtc ??= now) is var since && now - since >= NoReaderGrace)
                    {
                        _logger.LogInformation("RemoteDesk: {Path} {Seconds} sn okuyucusuz, durduruluyor", stream.MediaPath, NoReaderGrace.TotalSeconds);
                        await FinishAsync(stream, ScreenSessionStatus.Stopped, ScreenStopReason.NoViewers, null, notifyClient: true);
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    #endregion

    #region Yardımcılar (kilit içinde çağrılır)
    private async Task<ScreenStream> OpenStreamAsync(Guid deviceId, int monitorIndex, string connectionId, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        string path = PcMediaPath.Name(deviceId, monitorIndex);
        string ticket = await _tickets.IssuePublishAsync(path, sessionId, cancellationToken);

        var stream = new ScreenStream(sessionId, deviceId, monitorIndex, path, ticket, connectionId);
        await _store.CreateSessionAsync(sessionId, deviceId, monitorIndex, path, cancellationToken);
        _streams[(deviceId, monitorIndex)] = stream;

        await _hub.Clients.Client(connectionId).StartScreenStream(new StartScreenStreamCommand(sessionId, monitorIndex, PublishUrl(path, ticket), null));
        _logger.LogInformation("RemoteDesk: yayın komutu gönderildi {Path} oturum {SessionId}", path, sessionId);
        return stream;
    }

    /// <summary> Yol MediaMTX'te hazır olana kadar bekler (kilit DIŞINDA). <c>null</c> = hazır; aksi hâlde kullanıcıya gösterilecek neden. </summary>
    private async Task<string?> WaitReadyAsync(ScreenStream stream, CancellationToken cancellationToken)
    {
        if (stream.IsReady)
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadyTimeout);
        try
        {
            while (true)
            {
                if (stream.Ready.Task.IsCompleted)
                    return await stream.Ready.Task;

                var path = await WithGatewayAsync(g => g.GetRuntimePathAsync(stream.MediaPath, timeout.Token));
                if (path.IsSuccess && path.Data is { IsReady: true })
                {
                    if (stream.Ready.TrySetResult(null))
                        await _store.MarkStreamingAsync(stream.SessionId, CancellationToken.None);
                    return await stream.Ready.Task;
                }

                await Task.WhenAny(stream.Ready.Task, Task.Delay(ReadyPollInterval, timeout.Token));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            string reason = stream.LastState switch
            {
                ScreenStreamState.Retrying => stream.LastFailureReason is { } why
                    ? $"PC yayını başlatamadı: {why}"
                    : "PC yayını başlatamadı, yeniden deniyor. Birazdan tekrar deneyin.",
                null => "PC yanıt vermedi (yayın komutu alınmadı ya da kodlayıcı sınaması sürüyor).",
                _ => "PC'nin yayını zamanında hazır olmadı."
            };
            return reason;
        }
    }

    /// <summary> Kiralamayı bitirir; yayında izleyici kalmadıysa durdurma sayacı başlar. </summary>
    private async Task EndLeaseAsync(ViewLease lease)
    {
        _leases.Remove(lease.ViewId);
        if (lease.ViewLogId > 0)
            await _store.EndViewAsync(lease.ViewLogId);
        if (_leases.Values.All(l => l.Stream != lease.Stream))
            lease.Stream.LastViewerLeftUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Yayını bitirir: bilet silinir (yeniden bağlanamaz), kiralamalar biter, satır kapanır. <paramref name="notifyClient"/> ise istemciye durdurma
    /// gider ve yayın <see cref="KickAfter"/> boyunca "durduruluyor" kalır; değilse (istemci zaten bıraktı) hemen düşer.
    /// </summary>
    private async Task FinishAsync(ScreenStream stream, ScreenSessionStatus status, ScreenStopReason reason, string? failureReason, bool notifyClient)
    {
        if (stream.IsStopping)
            return;

        stream.StopRequestedUtc = DateTime.UtcNow;
        await _tickets.RevokePublishAsync(stream.MediaPath, stream.PublishTicket);

        foreach (var lease in _leases.Values.Where(l => l.Stream == stream).ToList())
            _leases.Remove(lease.ViewId);

        await _store.CloseSessionAsync(stream.SessionId, status, reason, failureReason);

        if (notifyClient)
        {
            await _hub.Clients.Client(stream.ConnectionId).StopScreenStream(new StopScreenStreamCommand(stream.SessionId));
        }
        else
        {
            _streams.Remove((stream.DeviceId, stream.MonitorIndex));
        }

        _logger.LogInformation("RemoteDesk: yayın bitti {Path} oturum {SessionId} — {Status}/{Reason} {Failure}",
            stream.MediaPath, stream.SessionId, status, reason, failureReason);
    }

    /// <summary> <c>rtsp://pc:{bilet}@merkez:8554/pc_…</c> — bilet parola alanında; MediaMTX onu auth kancasına iletir. </summary>
    private string PublishUrl(string path, string ticket) =>
        new UriBuilder(_options.PublishRtspBaseUrl) { UserName = "pc", Password = ticket, Path = path }.Uri.ToString();

    /// <summary> <c>IMediaGateway</c> scoped'dır; singleton koordinatör her çağrıyı kendi scope'unda yapar. </summary>
    private async Task<T> WithGatewayAsync<T>(Func<IMediaGateway, Task<T>> call)
    {
        using var scope = _scopes.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IMediaGateway>());
    }

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
    #endregion

    private sealed class ScreenStream(Guid sessionId, Guid deviceId, int monitorIndex, string mediaPath, string publishTicket, string connectionId)
    {
        public Guid SessionId { get; } = sessionId;
        public Guid DeviceId { get; } = deviceId;
        public int MonitorIndex { get; } = monitorIndex;
        public string MediaPath { get; } = mediaPath;
        public string PublishTicket { get; } = publishTicket;
        public string ConnectionId { get; } = connectionId;

        /// <summary> <c>null</c> = hazır, metin = başlatılamadı (neden). </summary>
        public TaskCompletionSource<string?> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsReady => Ready.Task.IsCompletedSuccessfully && Ready.Task.Result is null;

        public ScreenStreamState? LastState { get; set; }
        /// <summary> İstemcinin son bildirdiği hata/yeniden deneme nedeni. </summary>
        public string? LastFailureReason { get; set; }
        public string? Encoder { get; set; }
        public DateTime? LastViewerLeftUtc { get; set; }
        /// <summary> Yolun hazır olmasını bekleyen izleme isteği sayısı (kilit içinde değişir). </summary>
        public int PendingStarts { get; set; }
        public DateTime? NoReadersSinceUtc { get; set; }
        public DateTime? StopRequestedUtc { get; set; }
        public bool IsStopping => StopRequestedUtc is not null;
    }

    private sealed class ViewLease(Guid viewId, ScreenStream stream, Guid userId, DateTime lastRenewUtc)
    {
        public Guid ViewId { get; } = viewId;
        public ScreenStream Stream { get; } = stream;
        public Guid UserId { get; } = userId;
        public DateTime LastRenewUtc { get; set; } = lastRenewUtc;
        public long ViewLogId { get; set; }
    }
}
