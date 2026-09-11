using System.Threading.Channels;

namespace Scadex.Signalization.Runtime;

/// <summary> Dis kapi acilisinda cekilecek kare serisi. <paramref name="StartAtUtc"/> serinin T0'idir. </summary>
public sealed record EntrySnapshotJob(long SessionId, Guid CameraId, int Count, int IntervalMs, DateTime StartAtUtc);

/// <summary>
/// Kare serilerinin kuyrugu — SINGLETON. Seri motor seridinin DISINDA yurutulur: 5 x 1 sn'lik bekleme, ayni kabinin
/// sonraki olaylarini (kart okuma, kilit acma) geciktirmemeli.
/// </summary>
public sealed class EntrySnapshotQueue
{
    private readonly Channel<EntrySnapshotJob> _channel = Channel.CreateUnbounded<EntrySnapshotJob>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(EntrySnapshotJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<EntrySnapshotJob> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
