namespace Scadex.Business.Settings;

public class MediaGatewaySettings
{
    public const string SectionName = "MediaGateway";

    public int ApiTimeoutMs { get; set; } = 30000;
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:9997";
    public string WebRtcPublicBaseUrl { get; set; } = "http://127.0.0.1:8889";

    /// <summary> Tokenin omru (saniye). Kisa tutuluyor: token yalnizca el sikisma aninda kullanilir. </summary>
    public int TokenTtlSeconds { get; set; } = 60;

    /// <summary> 
    /// Son izleyici ilgili path'den ayrildiktan sonra MediaMTX'in kameraya olan RTSP oturumunu kapatmadan once bekledigi sure. Sekme yenilemede oturumun bastan kurulmasini engeller. 
    /// </summary>
    public string SourceOnDemandCloseAfter { get; set; } = "10s";

    /// <summary>
    /// MediaMTX'in kameraya baglanirken kullandigi RTSP tasima katmani. <c>tcp</c>, <c>udp</c> veya <c>multicast</c>. Cekimler de ayni yoldan
    /// okundugu icin onlar icin de gecerlidir; FFmpeg → MediaMTX baglantisi ise yereldir ve hep <c>tcp</c>'dir.
    /// </summary>
    public string RtspTransport { get; set; } = "tcp";
}
