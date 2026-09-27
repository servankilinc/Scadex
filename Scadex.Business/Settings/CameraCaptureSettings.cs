namespace Scadex.Business.Settings;

public class CameraCaptureSettings
{
    public const string SectionName = "Cameras";

    /// <summary>
    /// FFmpeg'in MediaMTX'in canli yolundan ilk kareyi almasi icin azami sure (kimse izlemiyorsa MediaMTX'in kameraya baglanmasi + ilk anahtar kare beklemesi). <para/>
    /// Anlik goruntude surecin tamami bu sureyle sinirlanir; asilirsa surec oldurulur. Klipte kayit suresinin UZERINE baglanti payi olarak eklenir
    /// ve RTSP soket zaman asimi (<c>-timeout</c>) olarak da verilir.
    /// </summary>
    public int SnapshotTimeoutMs { get; set; } = 5000;

    /// <summary> Es zamanli birden fazla istemciye tek istek uretsin diye, kısa bir değer verilir ki güncellik de sağlanabilsin. </summary>
    public int SnapshotCacheSeconds { get; set; } = 3;

    public string CaptureRoot { get; set; } = "uploads/captures";

    /// <summary>
    /// Saklama suresi <c>CameraCapture.ExpiresAt</c>'in yazma aninda hesaplarken kullanılacak parametre.
    /// <c>0</c> ise sinirsiz (<c>ExpiresAt = null</c>).
    /// </summary>
    public int CaptureRetentionDays { get; set; } = 30;

    /// <summary> Klip suresinin ust siniri. </summary>
    public int MaxClipDurationSec { get; set; } = 600;

    /// <summary>
    /// Kayit suresi dolduktan sonra FFmpeg'in MP4'u kapatmasi (faststart icin dosyayi yeniden yazmasi) icin taninan ek pay.
    /// Klibin zaman asimi = <see cref="SnapshotTimeoutMs"/> + sure + bu pay; asilirsa surec oldurulur ve cekim basarisiz sayilir.
    /// </summary>
    public int ClipFinalizeGraceMs { get; set; } = 3000;
}
