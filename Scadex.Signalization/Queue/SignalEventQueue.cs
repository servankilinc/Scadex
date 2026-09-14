using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Utils;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scadex.Signalization.Queue;

/// <summary> session/kind için kontrol et kuyruğu, SINGLETON olmak zorunda (kuyruğu kullanan istemci scoped'dir, kuyruk istekler arasi paylasilmalı). </summary>
public sealed class SignalEventQueue
{
    // SingleReader: kuyruğu tek bir background worker döngüsü okur
    private readonly Channel<SignalWorkItem> _channel = Channel.CreateUnbounded<SignalWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        }
    );

    // Zamanlayici her taramada aynı kararı yeniden üretir; islenmeyi bekleyen aynı iş kuyruğa ikinci kez girmez.
    private readonly ConcurrentDictionary<(long SessionId, SignalTimerKind Kind), byte> _pendingTimers = new();

    public void Enqueue(SignalWorkItem item) => _channel.Writer.TryWrite(item);

    /// <returns> Is zaten bekliyorsa <c>false</c>. </returns>
    public bool TryEnqueueTimer(TimerWork work)
    {
        if (!_pendingTimers.TryAdd((work.SessionId, work.Kind), 0))
            return false;

        _channel.Writer.TryWrite(work);
        return true;
    }

    public void CompleteTimer(TimerWork work) => _pendingTimers.TryRemove((work.SessionId, work.Kind), out _);

    public IAsyncEnumerable<SignalWorkItem> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
