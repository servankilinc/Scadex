using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary>
/// İç kapı kilidinin son bilinen durumu, scada output durumlarını vermiyor o yüzden biz takip ediyoruz. Veri yoksa kapi kilitli sayılır. 
/// Kapinin fiziksel açık/kapalı olduğu ise, switch kanalının <c>IoChannel.CurrentValue</c> alanında durur.
/// </summary>
public class SignalInnerDoorState : IEntity
{
    public Guid InnerDoorId { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    /// <summary> Son kilit komutunun <c>DeviceCommand.Id</c>'si </summary>
    public Guid? LastCommandId { get; set; }
}
