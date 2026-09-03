namespace Scadex.Business.Settings;

public class CameraCaptureSettings
{
    public const string SectionName = "Cameras";

    /// <summary>
    /// Anlık goruntu beklerken uygulanan zaman aşımı. <para/>
    /// <c>HttpClient.Timeout</c> ile arasındaki fark: <c>HttpClient.Timeout</c> tum request-response zincirini kapsar, bu ayar ise sadece goruntu cekimi icin gecen suredir. 
    /// Bu sureyi asan bir cekim, <c>CancellationToken</c> ile iptal edilir.
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
    /// Kayit suresinin uzerine eklenen pay: MediaMTX'in kameraya baglanmasi,
    /// ilk anahtar kareyi beklemesi ve segmenti kapatmasi zaman alir.
    /// </summary>
    public int ClipFinalizeGraceMs { get; set; } = 3000;
}
