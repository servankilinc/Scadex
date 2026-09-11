using Scadex.Business.Utils.ScadaEvents;
using Scadex.Model.Dtos.Scada.Events;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Runtime;

/// <summary>
/// Cekirdegin genel kancasina (<see cref="IScadaEventObserver"/>) takilan modul girisi. SICAK YOLDADIR: yalnizca
/// kuyruga birakir ve doner. Kabinin bu module ait olup olmadigina motor bakar — burada veritabani sorgusu yoktur.
/// </summary>
public sealed class SignalizationScadaObserver : IScadaEventObserver
{
    private readonly SignalEventQueue _queue;

    public SignalizationScadaObserver(SignalEventQueue queue) => _queue = queue;

    public Task OnChannelChangedAsync(ChannelChangedNotification notification, CancellationToken cancellationToken = default)
    {
        // Kapi anahtarlari dijital giristir; analog kanal ve cikis yankisi modulu ilgilendirmez.
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
