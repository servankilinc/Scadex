using Scadex.Model.Realtime.Events;
using System.Threading.Channels;

namespace Scadex.Signalization.Queue;

/// <summary>
/// Sanal kabin canlı yayın kuyruğu: çekirdekten gelen I/O kanal değişimleri (kapı anahtarı, siren, aydınlatma, kilit).
/// Engine <see cref="SignalEventQueue"/>'sundan AYRIDIR: engine bir SCADA komutunu işlerken kanal değişimleri beklemeyip ekran güncellemeleri hızlıca işlensin arkada.
/// SINGLETON olmak zorunda (yazan gözlemci scoped'dir).
/// </summary>
public sealed class SignalRealtimeQueue
{
    // SingleReader: kuyruğu tek bir background worker döngüsü okur
    private readonly Channel<ChannelChangedEvent> _channel = Channel.CreateUnbounded<ChannelChangedEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        }
    );

    public void Enqueue(ChannelChangedEvent notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<ChannelChangedEvent> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
