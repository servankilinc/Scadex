using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Media;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Streaming;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk.Hubs;

/// <summary>
/// Windows clients kontrol düzlemi — <c>/hubs/remote-desk/pc</c>, yalnızca modül açıkken eşlenir.
/// <para/>
/// TODO: clientaların JWT'si vs. yok endpointler AllowAnonymous kaldırılacak 
/// TODO: Pc kimliği <c>Device.MacAddress</c>'tir  "yalnızca MAC" kabul ediliyor bunun yerine güvenli bir yönteme geçilecek.
/// </summary>
[AllowAnonymous]
[DisableRateLimiting]
public class PcHub : Hub<IPcHubClient>
{
    private const string DeviceIdItem = "remotedesk.deviceId";

    private readonly IUnitOfWork _unitOfWork;
    private readonly PcConnectionRegistry _registry;
    private readonly RemoteDeskNetworkPolicy _networkPolicy;
    private readonly ScreenStreamCoordinator _streams;
    private readonly ILogger<PcHub> _logger;

    public PcHub(IUnitOfWork unitOfWork, PcConnectionRegistry registry, RemoteDeskNetworkPolicy networkPolicy, ScreenStreamCoordinator streams, ILogger<PcHub> logger)
    {
        _streams = streams;
        _unitOfWork = unitOfWork;
        _registry = registry;
        _networkPolicy = networkPolicy;
        _logger = logger;
    }

    /// <summary> MAC'leri aktif <c>DeviceType.Pc</c> cihazlarıyla eşler. </summary>
    public async Task<HelloResponse> Hello(HelloRequest request)
    {
        string? ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

        if (!_networkPolicy.IsAllowed(ip))
        {
            _logger.LogWarning("RemoteDesk: izinli ağ dışından bağlantı reddedildi {Ip} {MachineName}", ip, request.MachineName);
            return Reject(HelloStatus.NetworkNotAllowed);
        }

        var clientMacs = request.MacAddresses.Select(MacAddress.Normalize).OfType<string>().ToHashSet();

        // PC sayısı küçüktür: aday cihazlar projeksiyonla okunup bellekte normalize karşılaştırılır (DB'deki yazım serbest).
        var candidates = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.Id, d.Name, CabinetName = d.Cabinet!.Name, d.MacAddress },
            where: d => d.IsActive && d.MacAddress != null && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: Context.ConnectionAborted) ?? [];

        var matches = candidates
            .Where(d => MacAddress.Normalize(d.MacAddress) is { } mac && clientMacs.Contains(mac))
            .ToList();

        if (matches.Count == 0)
        {
            _logger.LogInformation("RemoteDesk: tanımsız PC {MachineName} {Ip} MAC'ler: {Macs}", request.MachineName, ip, string.Join(", ", clientMacs));
            return Reject(HelloStatus.UnknownDevice);
        }

        if (matches.Count > 1)
        {
            _logger.LogWarning("RemoteDesk: MAC'ler birden fazla PC cihazıyla eşleşti {MachineName} {Ip} {Devices}",
                request.MachineName, ip, string.Join(", ", matches.Select(m => $"{m.Name} ({m.MacAddress})")));
            return Reject(HelloStatus.Ambiguous, [.. matches.Select(m => m.MacAddress!)]);
        }

        var device = matches[0];
        var connection = new PcConnection(
            device.Id, Context.ConnectionId, request.ClientVersion, request.OsVersion, request.MachineName, request.UserName, ip,
            [.. clientMacs], request.Monitors ?? [], DateTime.UtcNow);

        if (!_registry.TryRegister(connection))
        {
            _registry.TryGet(device.Id, out var holder);
            _logger.LogWarning("RemoteDesk: {Device} zaten bağlı ({HolderMachine} {HolderIp}); yeni deneme reddedildi {MachineName} {Ip}",
                device.Name, holder?.MachineName, holder?.RemoteIp, request.MachineName, ip);
            return Reject(HelloStatus.AlreadyConnected, [device.MacAddress!]);
        }

        Context.Items[DeviceIdItem] = device.Id;
        _logger.LogInformation("RemoteDesk: {Device} ({Cabinet}) bağlandı — {MachineName} {Ip} sürüm {Version}, {MonitorCount} monitör",
            device.Name, device.CabinetName, request.MachineName, ip, request.ClientVersion, connection.Monitors.Count);

        return new HelloResponse(HelloStatus.Accepted, device.Id, device.Name, device.CabinetName, [device.MacAddress!]);
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(DeviceIdItem, out var value) && value is Guid deviceId)
        {
            // Önce yayınlar (bu bağlantıya ait olanlar), sonra kayıt: kayıt başka bağlantıya geçtiyse Remove false döner.
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

    private static HelloResponse Reject(HelloStatus status, string[]? matchedMacs = null) =>
        new(status, null, null, null, matchedMacs ?? []);
}
