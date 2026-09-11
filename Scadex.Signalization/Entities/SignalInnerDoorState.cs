using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Ic kapi kilidinin son bilinen durumu. Calisma durumudur: yalnizca motor, BASARILI komuttan sonra yazar.
/// Satir yoksa kapi kilitli sayilir. Kapinin acik/kapali oldugu burada DEGIL, anahtar kanalinin
/// <c>IoChannel.CurrentValue</c>'sunda durur — ayni bilgiyi iki yerde tutmamak icin.
/// </summary>
public class SignalInnerDoorState : IEntity
{
    public Guid InnerDoorId { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    /// <summary> Son kilit komutunun <c>DeviceCommand.Id</c>'si — FK DEGIL. </summary>
    public Guid? LastCommandId { get; set; }
}
