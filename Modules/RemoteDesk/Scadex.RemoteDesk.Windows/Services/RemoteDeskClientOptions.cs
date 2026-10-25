namespace Scadex.RemoteDesk.Windows.Services;

/// <summary> appsettings.json </summary>
public sealed class RemoteDeskClientOptions
{
    public const string Section = "RemoteDesk";

    /// <summary> Scadex merkezinin adresi (PcHub — Faz 3). </summary>
    public string CentralApiUrl { get; set; } = "";

    /// <summary>
    /// Basic Auth kullanıcı adı: diyagramdaki PC cihazının Id'si. Diyagramın özellik panelinde "Kullanıcı adı" olarak görünür.
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary> Basic Auth parolası: diyagramın özellik panelinde üretilen parola. Loglanmaz, arayüzde gösterilmez. </summary>
    public string DeviceSecret { get; set; } = "";
}
