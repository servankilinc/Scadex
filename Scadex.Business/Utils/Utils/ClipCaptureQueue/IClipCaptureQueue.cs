namespace CabinetOs.Business.Utils.ClipCaptureQueue;

/// <summary>
/// Klip cekimlerinin arka plan kuyrugu.
///
/// <b>Neden kuyruk:</b> klip, suresi kadar BEKLEMEK zorundadir (10 saniyelik bir
/// klip en az 10 saniye surer). HTTP istegini o kadar acik tutmak, istemciyi ve
/// istek havuzunu bosuna mesgul ederdi. Bunun yerine
/// <c>CameraCapture</c> satiri <c>Pending</c> olarak yazilip hemen donuluyor —
/// <c>CameraCapture.Status</c>'un XML dokumani <c>Pending</c>'in var olma
/// sebebini tam olarak bu senaryo diye anlatiyor.
///
/// <b>Kalici DEGIL.</b> Uygulama yeniden baslarsa kuyruktaki cekimler kaybolur
/// ve satirlari <c>Pending</c> olarak asili kalir. Kalici bir kuyruk (tablo +
/// toparlayici) bu turda bilincli olarak yazilmadi: elle tetiklenen, nadir ve
/// kullanicinin ekranda bekledigi bir istek soz konusu.
///
/// Port Business'ta, tuketen <c>ClipCaptureWorker</c> WebAPI'de —
/// <c>IDiagramNotifier</c> ile ayni desen.
/// </summary>
public interface IClipCaptureQueue
{
    /// <summary>Cekimi siraya alir. Bloklamaz.</summary>
    void Enqueue(long captureId);

    /// <summary>Siradaki cekimleri verir; kuyruk bosken bekler.</summary>
    IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken);
}
