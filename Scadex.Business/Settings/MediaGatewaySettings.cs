namespace Scadex.Business.Settings;

/// <summary>
/// MediaMTX'i WebAPI baslatir ve kapanirsa yeniden baslatir (<c>MediaMtxSupervisorWorker</c>); Degisiklik yeniden baslatma ister.
/// MediaMTX kendi <c>mediamtx.yml</c>'sini okur, Scadex appsettings.json Port/adres alanlari yml ile manuel senkron tutulur.
/// </summary>
public class MediaGatewaySettings
{
    public const string SectionName = "MediaGateway";

    /// <summary> MediaMTX Control API koku. <c>mediamtx.yml &gt; apiAddress</c> ile eslesmeli. </summary>
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:9997";

    public int ApiTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Sunucu ici okuyucunun (FFmpeg) baglandigi RTSP portu. <c>mediamtx.yml &gt; rtspAddress</c> ile eslesmeli.
    /// Host bilerek <c>127.0.0.1</c>'dir: MediaMTX ile API ayni sunucuda.
    /// </summary>
    public int RtspPort { get; set; } = 8554;

    /// <summary>
    /// Tarayicinin WHEP istegini attigi adres. Portu <c>mediamtx.yml &gt; webrtcAddress</c> ile eslesmeli (onde bir proxy yoksa);
    /// host disaridan erisilebilir olmali, dinleme adresinden farkli olabilir.
    /// </summary>
    public string WebRtcPublicBaseUrl { get; set; } = "http://127.0.0.1:8889";

    /// <summary> FFmpeg exe yolu. Goreliyse <c>ContentRootPath</c>'e gore cozulur; mutlak yol da verilebilir. </summary>
    public string FfmpegPath { get; set; } = "MediaTools/ffmpeg/ffmpeg.exe";

    /// <summary>
    /// MediaMTX exe yolu (<c>FfmpegPath</c> ile ayni kural). Yapilandirma, exe ile AYNI klasordeki <c>mediamtx.yml</c>'dir.
    /// </summary>
    public string MediaMtxPath { get; set; } = "MediaTools/mediamtx/mediamtx.exe";

    /// <summary> Tokenin omru (saniye). Kisa tutuluyor: token yalnizca el sikisma aninda kullanilir. </summary>
    public int TokenTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Son izleyici ilgili path'den ayrildiktan sonra MediaMTX'in kameraya olan RTSP oturumunu kapatmadan once bekledigi sure (saniye).
    /// Sekme yenilemede oturumun bastan kurulmasini engeller.
    /// </summary>
    public int SourceOnDemandCloseAfterSec { get; set; } = 10;

    /// <summary>
    /// MediaMTX'in kameraya baglanirken kullandigi RTSP tasima katmani. <c>tcp</c>, <c>udp</c>, <c>multicast</c> veya <c>automatic</c>. Cekimler de ayni yoldan
    /// okundugu icin onlar icin de gecerlidir; FFmpeg → MediaMTX baglantisi ise yereldir ve hep <c>tcp</c>'dir.
    /// </summary>
    public string RtspTransport { get; set; } = "tcp";
}
