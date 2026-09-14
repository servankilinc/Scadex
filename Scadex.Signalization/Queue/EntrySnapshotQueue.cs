using Scadex.Signalization.Model.Utils;
using System.Threading.Channels;

namespace Scadex.Signalization.Runtime;

/// <summary> Görüntü yakalama kuyruğu. Ayni kabinin sonraki eventleri (kart okuma, kilit acma) geciktirmemesei için ayrı bir kuyruk yapısı kurduk. </summary>
public sealed class EntrySnapshotQueue
{
    // SingleReader: kuyruğu tek bir background worker döngüsü okur
    private readonly Channel<EntrySnapshotJob> _channel = Channel.CreateUnbounded<EntrySnapshotJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        }
    );

    public void Enqueue(EntrySnapshotJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<EntrySnapshotJob> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
