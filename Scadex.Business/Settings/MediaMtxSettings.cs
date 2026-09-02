namespace CabinetOs.Business.Settings;

public class MediaMtxSettings
{
    public const string SectionName = "Mediamtx";

    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:9997";
    public string WebRtcPublicBaseUrl { get; set; } = "http://127.0.0.1:8889";

    /// <summary> Biletin omru (saniye). Kisa tutuluyor: bilet yalnizca el sikisma aninda kullanilir. </summary>
    public int TicketTtlSeconds { get; set; } = 60;

    /// <summary> 
    /// Son izleyici ilgili path'den ayrildiktan sonra MediaMTX'in kameraya olan RTSP oturumunu kapatmadan once bekledigi sure. Sekme yenilemede oturumun bastan kurulmasini engeller. 
    /// </summary>
    public string SourceOnDemandCloseAfter { get; set; } = "10s";

    /// <summary> RTSP tasima katmani. <c>tcp</c>, <c>udp</c> veya <c>multicast</c> </summary>
    public string RtspTransport { get; set; } = "tcp";

    /// <summary>
    /// MediaMTX'in klip segmentlerini yazdigi gecici kok dizin.
    ///
    /// <b>MediaMTX ile bu uygulama ayni dosya sistemini gormek ZORUNDA</b> —
    /// klip akisi, MediaMTX'in yazdigi dosyayi bu uygulamanin okumasina dayanir.
    /// Ikisi ayri makinede calisacaksa klip cekimi calismaz.
    /// </summary>
    public string RecordRoot { get; set; } = "";

    /// <summary>
    /// <c>policy_mediamtx_auth</c> icin 10 saniyelik butce. Varsayilan politika
    /// (50/10 sn) yetmez: bir grid sayfasi yenilendiginde MediaMTX kutucuk basina
    /// bir kanca cagrisi yapar ve hepsi tek IP (loopback) partition'ina duser.
    /// </summary>
    public int AuthRateLimitPermitsPer10Seconds { get; set; } = 300;
}
