namespace Scadex.RemoteDesk;

/// <summary> appsettings.json </summary>
public sealed class RemoteDeskOptions
{
    public const string SectionName = "Modules:RemoteDesk";

    /// <summary> Modül kurulum başına açılır; tanımsız = kapalı. </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Windows istemcisine verilen yayın adresinin kökü, ör. <c>rtsp://10.0.0.5:8554</c>. PC'lerin MediaMTX'e ulaştığı adrestir;
    /// <c>mediamtx.yml &gt; rtspAddress</c> portuyla eşleşmeli. Sunucu içi <c>LiveRtspUrl</c>'den (<c>127.0.0.1</c>) bilerek ayrıdır.
    /// </summary>
    public string PublishRtspBaseUrl { get; set; } = "rtsp://127.0.0.1:8554";

    /// <summary>
    /// Yayın (publish) ve PC bağlantısının kabul edildiği ağlar (CIDR, ör. <c>10.0.0.0/8</c>). Boş = her yerden
    /// ("yalnızca MAC" kararının bedava hafifletmesi, RemoteDesk.md § 5.3).
    /// </summary>
    public string[] AllowedNetworks { get; set; } = [];
}
