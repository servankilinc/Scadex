using Scadex.Core.Utils.ResultPattern;
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


/// <summary> Bir kullanıcının bu kabine gönderdiği elle output komutu sonuçlandı — açık işlemin zaman çizelgesine yazılır. </summary>
public sealed class ManualCommandWork : SignalWorkItem
{
    public ManualCommandWork(OutputCommandNotification notification) : base(notification.CabinetId) => Notification = notification;

    public OutputCommandNotification Notification { get; set; }
}


/// <summary>
/// Yetkili kullanıcının açık işlemi elle kapatma talebi. HTTP isteği sonucu <see cref="Completion"/> üzerinden bekler;
/// <c>SignalEventWorker</c> iş hata verse de tamamlar, istek asılı kalmaz.
/// </summary>
public sealed class ManualCloseWork : SignalWorkItem
{
    public ManualCloseWork(Guid cabinetId, long sessionId, Guid userId, string reason) : base(cabinetId)
    {
        SessionId = sessionId;
        UserId = userId;
        Reason = reason;
    }

    public long SessionId { get; }
    public Guid UserId { get; }
    public string Reason { get; }

    public TaskCompletionSource<Result> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
