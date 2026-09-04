using System.Threading.Channels;

namespace Scadex.Business.Utils.ClipCaptureQueue;

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
