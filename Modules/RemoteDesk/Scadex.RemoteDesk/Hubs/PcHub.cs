using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.Auth;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Control;
using Scadex.RemoteDesk.Media;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Streaming;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk.Hubs;

/// <summary>
/// Windows clients kontrol düzlemi — <c>/hubs/remote-desk/pc</c>, yalnızca modül açıkken eşlenir.
/// <para/>
/// PC Basic Auth ile doğrulanır (çekirdeğin <see cref="DeviceAuthPolicies.Pc"/> policy'si; kullanıcı adı = cihaz Id'si, parola =
/// diyagramda üretilen parola).
/// </summary>
[Authorize(Policy = DeviceAuthPolicies.Pc)]
[DisableRateLimiting]
public class PcHub : Hub<IPcHubClient>
{
    private const string DeviceIdItem = "remotedesk.deviceId";

    private readonly IUnitOfWork _unitOfWork;
    private readonly PcConnectionRegistry _registry;
    private readonly RemoteDeskNetworkPolicy _networkPolicy;
    private readonly ScreenStreamCoordinator _streams;
    private readonly RemoteControlCoordinator _control;
    private readonly FileTransfer.FileTransferCoordinator _files;
    private readonly ILogger<PcHub> _logger;

    public PcHub(IUnitOfWork unitOfWork, PcConnectionRegistry registry, RemoteDeskNetworkPolicy networkPolicy, ScreenStreamCoordinator streams,
        RemoteControlCoordinator control, FileTransfer.FileTransferCoordinator files, ILogger<PcHub> logger)
    {
        _streams = streams;
        _control = control;
        _files = files;
        _unitOfWork = unitOfWork;
        _registry = registry;
        _networkPolicy = networkPolicy;
        _logger = logger;
    }

    /// <summary> Kimliği doğrulanmış PC'yi bellekteki bağlantı kaydına yazar (cihaz başına ilk bağlanan kazanır). </summary>
    public async Task<HelloResponse> Hello(HelloRequest request)
    {
        string? ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

        if (!_networkPolicy.IsAllowed(ip))
        {
            _logger.LogWarning("RemoteDesk: izinli ağ dışından bağlantı reddedildi {Ip} {MachineName}", ip, request.MachineName);
            return Reject(HelloStatus.NetworkNotAllowed);
        }

        // Policy kimliği zorladı: cihaz aktif ve tipi Pc olduğu negotiate'te doğrulandı. Burada yalnızca gösterim alanları okunur.
        Guid deviceId = Context.User?.GetDeviceId() ?? throw new HubException("PC kimliği yok.");
        var device = await _unitOfWork.Devices.GetAsync(
            select: d => new { d.Name, CabinetName = d.Cabinet!.Name },
            where: d => d.Id == deviceId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: Context.ConnectionAborted);
        if (device is null)
            throw new HubException("PC bulunamadı.");   // negotiate ile Hello arasında cihaz silindi

        var connection = new PcConnection(
            deviceId, Context.ConnectionId, request.ClientVersion, request.OsVersion, request.MachineName, request.UserName, ip,
            request.Monitors ?? [], DateTime.UtcNow);

        if (!_registry.TryRegister(connection))
        {
            _registry.TryGet(deviceId, out var holder);
            _logger.LogWarning("RemoteDesk: {Device} zaten bağlı ({HolderMachine} {HolderIp}); yeni deneme reddedildi {MachineName} {Ip}",
                device.Name, holder?.MachineName, holder?.RemoteIp, request.MachineName, ip);
            return Reject(HelloStatus.AlreadyConnected);
        }

        Context.Items[DeviceIdItem] = deviceId;
        _logger.LogInformation("RemoteDesk: {Device} ({Cabinet}) bağlandı — {MachineName} {Ip} sürüm {Version}, {MonitorCount} monitör",
            device.Name, device.CabinetName, request.MachineName, ip, request.ClientVersion, connection.Monitors.Count);

        return new HelloResponse(HelloStatus.Accepted, deviceId, device.Name, device.CabinetName);
    }

    /// <summary> Monitör takıldı/çıkarıldı ya da çözünürlük değişti. </summary>
    public Task ReportMonitors(MonitorInfo[] monitors)
    {
        _registry.UpdateMonitors(RequireDeviceId(), Context.ConnectionId, monitors);
        return Task.CompletedTask;
    }

    /// <summary> Yayın durumu (başladı, yeniden deniyor, ekran kilitli, başarısız…). Yalnızca durum değişince gelir. </summary>
    public Task ReportStreamState(StreamStateReport report) =>
        _streams.OnStreamStateAsync(RequireDeviceId(), Context.ConnectionId, report);

    /// <summary> Dosya transferinin sonucu (push/pull bitti ya da reddedildi). </summary>
    public Task ReportFileTransfer(FileTransferResult result) =>
        _files.OnPcResultAsync(RequireDeviceId(), Context.ConnectionId, result);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(DeviceIdItem, out var value) && value is Guid deviceId)
        {
            // Önce kontrol ve yayınlar (bu bağlantıya ait olanlar), sonra kayıt: kayıt başka bağlantıya geçtiyse Remove false döner.
            await _control.OnPcDisconnectedAsync(deviceId, Context.ConnectionId);
            await _streams.OnClientDisconnectedAsync(deviceId, Context.ConnectionId);
            if (_registry.Remove(deviceId, Context.ConnectionId))
                _logger.LogInformation("RemoteDesk: {DeviceId} bağlantısı koptu ({Reason})", deviceId, exception?.Message ?? "istemci kapattı");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid RequireDeviceId() =>
        Context.Items.TryGetValue(DeviceIdItem, out var value) && value is Guid deviceId
            ? deviceId
            : throw new HubException("Önce Hello kabul edilmeli.");

    private static HelloResponse Reject(HelloStatus status) => new(status, null, null, null);
}
