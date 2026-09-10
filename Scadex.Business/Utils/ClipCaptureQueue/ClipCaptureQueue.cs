using System.Threading.Channels;

namespace Scadex.Business.Utils.ClipCaptureQueue;

public class ClipCaptureQueue : IClipCaptureQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
    {
        // Kanaldan okuyan TEK bir dongu var (ClipCaptureWorker); bunu bildirmek  kanalin daha ucuz bir yol secmesini saglar.
        // Birden fazla dongu ayni kanali okumaya baslarsa bu bayrak KALDIRILMALI.
        SingleReader = true
    });

    /// <inheritdoc />
    public void Enqueue(long captureId) => 
        _channel.Writer.TryWrite(captureId);

    /// <inheritdoc />
    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
