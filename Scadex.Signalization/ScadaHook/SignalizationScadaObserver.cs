using Scadex.Business.Utils.ScadaObserver;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Queue;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.ScadaHook;

/// <summary> Scadex çekirdeği tarafından tetiklenen eventleri dinleyen hook servismiz. Yalnızca kuyruğa bırakır ve döner </summary>
public sealed class SignalizationScadaObserver : IScadaEventObserver
{
    private readonly SignalEventQueue _queue;

    public SignalizationScadaObserver(SignalEventQueue queue) => _queue = queue;

    public Task OnChannelChangedAsync(ChannelChangedNotification notification, CancellationToken cancellationToken = default)
    {
        // Input modülününe bağlı "Analog Input" ve "Output(zaten output değişimleri Scadex çekirdeğine de düşmüyor ingest olarak)" sinyalizasyon modülünü ilgilendirmez.
        // Kapı switch'leri "Dijital Input"
        if (notification.Direction == PinDirection.Input)
            _queue.Enqueue(new ChannelChangedWork(notification));

        return Task.CompletedTask;
    }

    public Task OnCardPresentedAsync(CardPresentedNotification notification, CancellationToken cancellationToken = default)
    {
        _queue.Enqueue(new CardPresentedWork(notification));
        return Task.CompletedTask;
    }
}
