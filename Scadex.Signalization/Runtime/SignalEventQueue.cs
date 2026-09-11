using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scadex.Signalization.Runtime;

/// <summary>
/// Motorun giris kuyrugu — SINGLETON olmak zorunda (gozlemci scoped'dir, kuyruk istekler arasi paylasilir).
/// Bellek icidir: uygulama yeniden baslarsa bekleyen olay kaybolur (klip kuyrugundaki kabulle ayni). Oturum durumu
/// veritabaninda durdugu icin zamanlayici isleri kaybolmaz, bir sonraki taramada yeniden uretilir.
/// </summary>
public sealed class SignalEventQueue
{
    // SingleReader: kuyrugu tek bir dagitim dongusu okur; paralellik okumada degil, kabin seritlerinde.
    private readonly Channel<SignalWorkItem> _channel = Channel.CreateUnbounded<SignalWorkItem>(new UnboundedChannelOptions { SingleReader = true });

    // Zamanlayici her taramada ayni karari yeniden uretir; islenmeyi bekleyen ayni is kuyruga ikinci kez girmez.
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

    /// <summary> Motor zamanlayici isini bitirdiginde cagrilir; ayni karar sonraki taramada yeniden uretilebilir. </summary>
    public void CompleteTimer(TimerWork work) => _pendingTimers.TryRemove((work.SessionId, work.Kind), out _);

    public IAsyncEnumerable<SignalWorkItem> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
