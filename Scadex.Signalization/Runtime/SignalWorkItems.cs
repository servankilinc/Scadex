using Scadex.Model.Dtos.Scada.Events;

namespace Scadex.Signalization.Runtime;

/// <summary> Motorun isledigi tek birim. Ayni kabinin isleri SIRAYLA islenir (<see cref="SignalEventQueue"/>). </summary>
public abstract record SignalWorkItem(Guid CabinetId);

public sealed record ChannelChangedWork(ChannelChangedNotification Notification) : SignalWorkItem(Notification.CabinetId);

public sealed record CardPresentedWork(CardPresentedNotification Notification) : SignalWorkItem(Notification.CabinetId);

public enum SignalTimerKind
{
    /// <summary> Oturumun siren talebinin suresi doldu. </summary>
    SirenDue = 1,
    /// <summary> Dis kapi acildiktan sonra yetkili kart suresi doldu. </summary>
    AwaitingCardDue = 2,
    /// <summary> Oturum azami suresini asti. </summary>
    MaxDurationDue = 3
}

/// <summary>
/// Zamanlayicinin karari. Zamanlayici isi KENDISI yapmaz, ayni kabin kuyruguna birakir: boylece zamanlayici ile
/// ingest ayni oturum uzerinde yarismaz. Motor kosulu yeniden kontrol eder (islem sirasinda durum degismis olabilir).
/// </summary>
public sealed record TimerWork(Guid CabinetId, long SessionId, SignalTimerKind Kind) : SignalWorkItem(CabinetId);
