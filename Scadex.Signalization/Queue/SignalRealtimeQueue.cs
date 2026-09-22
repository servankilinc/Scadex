using Scadex.Model.Dtos.Scada.Events;
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
    private readonly Channel<ChannelChangedNotification> _channel = Channel.CreateUnbounded<ChannelChangedNotification>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        }
    );

    public void Enqueue(ChannelChangedNotification notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<ChannelChangedNotification> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
