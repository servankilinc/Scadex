namespace Scadex.RemoteDesk.Windows.Services;

/// <summary> appsettings.json </summary>
public sealed class RemoteDeskClientOptions
{
    public const string Section = "RemoteDesk";

    /// <summary> Scadex merkezinin adresi (PcHub — Faz 3). </summary>
    public string CentralApiUrl { get; set; } = "";

    /// <summary>
    /// Saha test yayınının varsayılan RTSP adresi; <c>{monitor}</c> monitör sırasıyla değiştirilir. Merkez gelene
    /// kadar yalnızca test içindir — üretimde yayın adresi ve bileti sunucudan gelir.
    /// </summary>
    public string TestPublishUrl { get; set; } = "";
}
