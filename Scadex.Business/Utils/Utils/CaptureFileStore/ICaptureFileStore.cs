using CabinetOs.Core.Utils.ResultPattern;

namespace CabinetOs.Business.Utils.CaptureFileStore;

/// <summary>Diske yazilan cekimin sonucu.</summary>
/// <param name="RelativePath">
/// <c>wwwroot</c>'a gore GORELI yol (orn. <c>uploads/captures/2026/08/31/{guid}.jpg</c>).
/// Tam URL degil: sema, host ve kok dizin yapilandirmadan gelir; her satira
/// yazilsaydi depo tasindiginda binlerce satir guncellenmeliydi.
/// </param>
public sealed record StoredCapture(string RelativePath, long SizeBytes);

/// <summary>
/// Cekim dosyalarinin diske yazilmasi.
///
/// <b>Arayuz Business'ta, implementasyon WebAPI'de</b> —
/// <c>TemplateImageStore</c> ile birebir ayni gerekce: <c>IWebHostEnvironment</c>
/// ve <c>wwwroot</c> barindirma detaylaridir ve <c>CameraService</c>'i dosya
/// sistemine baglamak onu test edilemez hale getirirdi.
///
/// <b>Sonucu acikca:</b> <c>wwwroot</c> altindaki dosyalar
/// <c>UseStaticFiles</c> ile KIMLIK DOGRULAMASIZ servis edilir; URL'yi bilen
/// goruntuyu indirebilir. Tek engel dosya adinin tahmin edilemez bir
/// <c>Guid</c> olmasidir. Bu desen <c>CameraCapture.RelativePath</c>'in XML
/// dokumaninda zaten ilan edilmis ve sistem kapali agda calisiyor. Yetkili bir
/// uctan servis istenirse degisecek tek yer bu sinifin kok dizinidir.
/// </summary>
public interface ICaptureFileStore
{
    /// <summary>Anlik goruntuyu tarihli klasore yazar.</summary>
    Task<Result<StoredCapture>> SaveSnapshotAsync(byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Medya gecidinin urettigi klip dosyasini kalici konuma tasir.
    /// Kopyalamaz: dosya buyuk olabilir ve kaynak zaten gecici bir dizindedir.
    /// </summary>
    Task<Result<StoredCapture>> MoveClipAsync(string sourceFullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Medya gecidinin klip icin kullandigi gecici klasoru siler. Hata FIRLATMAZ:
    /// temizlik basarisizligi, tamamlanmis bir cekimi basarisiz gostermemeli.
    /// </summary>
    void TryDeleteDirectory(string fullPath);
}
