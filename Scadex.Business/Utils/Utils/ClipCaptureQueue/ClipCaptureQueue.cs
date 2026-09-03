using System.Threading.Channels;

namespace CabinetOs.Business.Utils.ClipCaptureQueue;

/// <summary>
/// <see cref="IClipCaptureQueue"/>'nun bellek ici implementasyonu.
///
/// Sinirsiz kapasite: uretici ucu zaten kullanicinin elle bastigi bir dugmedir
/// ve <c>MaxClipDurationSec</c> ile sinirli. Sinirli bir kanal, kuyruk dolunca
/// istegi ya bloklar ya da dusururdu; ikisi de burada gereksiz karmasiklik.
/// </summary>
public class ClipCaptureQueue : IClipCaptureQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
    {
        // Tek tuketici var (ClipCaptureWorker); bunu bildirmek kanalin daha ucuz
        // bir yol secmesini saglar.
        SingleReader = true
    });

    public void Enqueue(long captureId) => _channel.Writer.TryWrite(captureId);

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
