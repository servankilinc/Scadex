namespace Scadex.RemoteDesk.Windows.Services.Connection;

public enum ConnectionState
{
    /// <summary> <c>appsettings.json &gt; RemoteDesk</c>'te merkez adresi ya da cihaz kimliği (Id + sır) boş. </summary>
    NotConfigured,
    Connecting,
    /// <summary> Kimlik doğrulandı, <c>Hello</c> kabul edildi: bu PC merkezdeki cihazına bağlı. </summary>
    Connected,
    /// <summary> Merkez cihaz kimliğini reddetti (401): Id yanlış, sır yanlış/yenilenmiş/iptal edilmiş ya da cihaz silinmiş. </summary>
    Unauthorized,
    /// <summary> Cihaz başka bir bağlantıda (ilk bağlanan kazanır). </summary>
    AlreadyConnected,
    /// <summary> Merkez bu ağdan bağlantı kabul etmiyor (<c>AllowedNetworks</c>). </summary>
    NetworkNotAllowed,
    /// <summary> Merkeze ulaşılamadı ya da bağlantı koptu; yeniden denenecek. </summary>
    Disconnected
}

/// <param name="Detail">Sahada okunacak açıklama (son hata, bekleme süresi…).</param>
public sealed record ConnectionStatus(
    ConnectionState State,
    string CentralUrl,
    string Detail,
    string? DeviceName = null,
    string? CabinetName = null);
