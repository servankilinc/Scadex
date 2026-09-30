namespace Scadex.RemoteDesk.Windows.Services.Connection;

public enum ConnectionState
{
    /// <summary> <c>appsettings.json &gt; RemoteDesk:CentralApiUrl</c> boş. </summary>
    NotConfigured,
    Connecting,
    /// <summary> <c>Hello</c> kabul edildi: bu PC merkezde bir cihaza eşlendi. </summary>
    Connected,
    /// <summary> Bu PC'nin MAC'leri merkezde hiçbir PC cihazında yok. </summary>
    UnknownDevice,
    /// <summary> MAC'ler birden fazla cihazla eşleşti. </summary>
    Ambiguous,
    /// <summary> Cihaz başka bir bağlantıda (ilk bağlanan kazanır). </summary>
    AlreadyConnected,
    /// <summary> Merkez bu ağdan bağlantı kabul etmiyor (<c>AllowedNetworks</c>). </summary>
    NetworkNotAllowed,
    /// <summary> Merkeze ulaşılamadı ya da bağlantı koptu; yeniden denenecek. </summary>
    Disconnected
}

/// <param name="Detail">Sahada okunacak açıklama (son hata, bekleme süresi…).</param>
/// <param name="MatchedMacs">Merkezin eşleştirdiği / çakışan MAC'ler (gösterim biçiminde).</param>
public sealed record ConnectionStatus(
    ConnectionState State,
    string CentralUrl,
    string Detail,
    Guid? DeviceId = null,
    string? DeviceName = null,
    string? CabinetName = null,
    IReadOnlyList<string>? MatchedMacs = null);
