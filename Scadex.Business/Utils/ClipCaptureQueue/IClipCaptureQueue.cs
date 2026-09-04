namespace Scadex.Business.Utils.ClipCaptureQueue;

/// <summary>
/// Klip cekimlerinin arka plan kuyrugu.
/// HTTP istegini klip, suresi kadar acik tutmak, istemciyi ve istek havuzunu bosuna mesgul ederdi. 
/// Bunun yerine <c>CameraCapture</c>.<c>Pending</c> olarak yazilip hemen cevap dönülür
/// </summary>
public interface IClipCaptureQueue
{
    /// <summary>Çekimi sıraya alır.</summary>
    void Enqueue(long captureId);

    /// <summary>Siradaki çekimleri verir; kuyruk boşken bekler.</summary>
    IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken);
}
