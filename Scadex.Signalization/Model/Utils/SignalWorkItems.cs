using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Queue;

namespace Scadex.Signalization.Model.Utils;


/// <summary> Sinayizasyon modülünün işlediği bir birim iş. Aynı kabinin işleri sırayla işlenir (<see cref="SignalEventQueue"/>). </summary>
public abstract class SignalWorkItem
{
    public SignalWorkItem(Guid cabinetId) => CabinetId = cabinetId;

    public Guid CabinetId { get; set; }
};


/// <summary> Kanal değişim işi </summary>
public sealed class ChannelChangedWork : SignalWorkItem
{
    public ChannelChangedWork(ChannelChangedNotification notification) : base(notification.CabinetId) => Notification = notification;

    public ChannelChangedNotification Notification { get; set; }
}


/// <summary> Kart okutulma işi </summary>
public sealed class CardPresentedWork : SignalWorkItem
{
    public CardPresentedWork(CardPresentedNotification notification) : base(notification.CabinetId) => Notification = notification;

    public CardPresentedNotification Notification { get; set; }
}


/// <summary> Zamanlayıcının kararı işi </summary>
public sealed class TimerWork : SignalWorkItem
{
    public TimerWork(Guid cabinetId, long sessionId, SignalTimerKind kind) : base(cabinetId)
    {
        SessionId = sessionId;
        Kind = kind;
    }
    public long SessionId { get; set; }
    public SignalTimerKind Kind { get; set; }
}
