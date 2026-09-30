using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Model.Dtos.Camera.Commands;

namespace Scadex.RemoteDesk.Media;

/// <summary> MediaMTX auth kancasında <c>pc_*</c> yollarını yetkilendirir <c>read</c> → okuma tokenı, <c>publish</c> → <c>AllowedNetworks</c> + yayın tokenı, diğerleri reddedilir </summary>
public sealed class PcMediaPathAuthorizer : IMediaPathAuthorizer
{
    private readonly ScreenTicketStore _tickets;
    private readonly RemoteDeskNetworkPolicy _networkPolicy;
    private readonly ILogger<PcMediaPathAuthorizer> _logger;

    public PcMediaPathAuthorizer(ScreenTicketStore tickets, RemoteDeskNetworkPolicy networkPolicy, ILogger<PcMediaPathAuthorizer> logger)
    {
        _tickets = tickets;
        _networkPolicy = networkPolicy;
        _logger = logger;
    }

    public bool CanHandle(string path) => PcMediaPath.IsPcPath(path);

    public async Task<bool> AuthorizeAsync(MediaMtxAuthDto request, CancellationToken cancellationToken = default)
    {
        string path = request.Path!;
        if (string.IsNullOrEmpty(request.Password))
            return false;

        if (string.Equals(request.Action, "read", StringComparison.OrdinalIgnoreCase))
            return await _tickets.ValidateReadAsync(path, request.Password, cancellationToken);

        if (string.Equals(request.Action, "publish", StringComparison.OrdinalIgnoreCase))
        {
            if (!_networkPolicy.IsAllowed(request.Ip))
            {
                _logger.LogWarning("RemoteDesk yayını izinli ağ dışından reddedildi: {Path} {Ip}", path, request.Ip);
                return false;
            }

            bool valid = await _tickets.ValidatePublishAsync(path, request.Password, cancellationToken);
            if (!valid)
                _logger.LogWarning("RemoteDesk yayını geçersiz biletle reddedildi: {Path} {Ip}", path, request.Ip);
            return valid;
        }

        return false;
    }
}

/// <summary> <c>Modules:RemoteDesk:AllowedNetworks</c> (CIDR). Boş liste = her yer. Açılışta bir kez ayrıştırılır. </summary>
public sealed class RemoteDeskNetworkPolicy
{
    private readonly IPNetwork[] _networks;

    public RemoteDeskNetworkPolicy(IOptions<RemoteDeskOptions> options)
    {
        _networks = [.. options.Value.AllowedNetworks.Select(n => IPNetwork.TryParse(n.Trim(), out var network)
            ? network
            : throw new InvalidOperationException($"Modules:RemoteDesk:AllowedNetworks geçersiz CIDR: '{n}'"))];
    }

    public bool IsAllowed(string? ip)
    {
        if (_networks.Length == 0)
            return true;
        if (!IPAddress.TryParse(ip, out var address))
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return _networks.Any(n => n.Contains(address));
    }
}
