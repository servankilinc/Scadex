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
    private readonly SignalRealtimeQueue _realtimeQueue;

    public SignalizationScadaObserver(SignalEventQueue queue, SignalRealtimeQueue realtimeQueue)
    {
        _queue = queue;
        _realtimeQueue = realtimeQueue;
    }

    public Task OnChannelChangedAsync(ChannelChangedNotification notification, CancellationToken cancellationToken = default)
    {
        switch (notification.Direction)
        {
            // Kapı switch'leri "Dijital Input" ve clinetl'lar ui günceller.
            case PinDirection.Input:
                _queue.Enqueue(new ChannelChangedWork(notification));
                _realtimeQueue.Enqueue(notification);
                break;

            // Siren / aydınlatma / kilit: başarılı komut kanalın değerini değiştirdi işlenecek bir event yok ve clinetl'lar ui günceller.
            case PinDirection.Output:
                _realtimeQueue.Enqueue(notification);
                break;

            // "Analog Input" sinyalizasyon modülünü ilgilendirmez.
        }

        return Task.CompletedTask;
    }

    public Task OnCardPresentedAsync(CardPresentedNotification notification, CancellationToken cancellationToken = default)
    {
        _queue.Enqueue(new CardPresentedWork(notification));
        return Task.CompletedTask;
    }

    public Task OnOutputCommandCompletedAsync(OutputCommandNotification notification, CancellationToken cancellationToken = default)
    {
        // Göndereni olmayan komut motorun kendisidir (arka plan işinde HttpContext yok); motor o komutların olayını zaten yazıyor.
        // Kullanıcının gönderdiği (sanal kabin / diyagram) komut kabin şeridine girer: açık işlem varsa zaman çizelgesine yazılır.
        if (notification.RequestedByUserId != null)
            _queue.Enqueue(new ManualCommandWork(notification));

        return Task.CompletedTask;
    }
}
