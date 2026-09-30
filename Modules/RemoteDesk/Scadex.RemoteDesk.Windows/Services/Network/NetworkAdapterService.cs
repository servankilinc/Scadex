using System.Net.NetworkInformation;
using Scadex.RemoteDesk.Contracts.Hub;

namespace Scadex.RemoteDesk.Windows.Services.Network;

/// <summary> Bir fiziksel ağ kartı. <c>Mac</c> normalizedir (12 onaltılık hane, büyük harf). </summary>
public sealed record NetworkAdapter(string Name, string Description, string Mac, bool IsUp)
{
    public string DisplayMac => MacAddress.Format(Mac);
}

public interface INetworkAdapterService
{
    /// <summary> Merkeze gönderilen kartlar. Her çağrıda yeniden okunur — yalnızca bağlanırken çağrılır (zamanlayıcı yok). </summary>
    IReadOnlyList<NetworkAdapter> GetPhysicalAdapters();
}

/// <summary>
/// PC'nin kimliği <c>Device.MacAddress</c>'tir; bu servis hangi MAC'lerin gönderileceğini seçer.
/// Kapalı (kablosu takılı olmayan) kartlar da gönderilir: teknisyen diyagrama Ethernet MAC'ini girmiş, PC o an Wi-Fi'da olabilir.
/// Sanal kartlar (Hyper-V, VPN/TAP, Docker, VirtualBox, VMware, Bluetooth, loopback) dışarıda — makineler arasında
/// tekrarlanabilir ya da değişebilir adresleri vardır.
/// </summary>
public sealed class NetworkAdapterService : INetworkAdapterService
{
    private static readonly string[] VirtualMarkers =
    [
        "virtual", "hyper-v", "vethernet", "vmware", "virtualbox", "vbox", "tap-", "tap adapter", "vpn", "wan miniport",
        "bluetooth", "loopback", "npcap", "docker", "wsl", "tunnel", "teredo", "wireguard", "tailscale", "zerotier"
    ];

    public IReadOnlyList<NetworkAdapter> GetPhysicalAdapters()
    {
        return [.. NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Wireless80211)
            .Where(n => !IsVirtual(n))
            .Select(n => (Adapter: n, Mac: MacAddress.Normalize(n.GetPhysicalAddress().ToString())))
            .Where(x => x.Mac is not null && x.Mac != "000000000000")
            .Select(x => new NetworkAdapter(x.Adapter.Name, x.Adapter.Description, x.Mac!, x.Adapter.OperationalStatus == OperationalStatus.Up))
            .DistinctBy(a => a.Mac)
            .OrderByDescending(a => a.IsUp).ThenBy(a => a.Name)];
    }

    private static bool IsVirtual(NetworkInterface n)
    {
        string text = (n.Name + " " + n.Description).ToLowerInvariant();
        return VirtualMarkers.Any(text.Contains);
    }
}
