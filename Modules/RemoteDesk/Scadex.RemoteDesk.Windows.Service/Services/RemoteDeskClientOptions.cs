namespace Scadex.RemoteDesk.Windows.Service.Services;

/// <summary> appsettings.json </summary>
public sealed class RemoteDeskClientOptions
{
    public const string Section = "RemoteDesk";

    /// <summary> Scadex merkezinin adresi. </summary>
    public string CentralApiUrl { get; set; } = "";

    /// <summary>
    /// Basic Auth kullanıcı adı: diyagramdaki PC cihazının Id'si. Diyagramın özellik panelinde "Kullanıcı adı" olarak görünür.
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary> Basic Auth parolası: diyagramın özellik panelinde üretilen parola. Loglanmaz, arayüzde gösterilmez. </summary>
    public string DeviceSecret { get; set; } = "";

    /// <summary>
    /// <c>Authorization</c> başlığının değeri (<c>Basic base64(DeviceId:DeviceSecret)</c>) — <c>PcHub</c> ve dosya transferinin
    /// <c>push</c>/<c>pull</c> uçları aynı kimliği ister. Sır loglanmaz, arayüze gitmez.
    /// </summary>
    public string BasicAuthorization() =>
        "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{DeviceId.Trim()}:{DeviceSecret.Trim()}"));
}
