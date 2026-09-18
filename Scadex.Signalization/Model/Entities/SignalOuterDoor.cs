using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary>
/// <strong> SANAL Dış Kapı: </strong>
/// <list type="bullet">
/// <item> Kapının anahtar (switch) input kanalı <c>IoChannel</c> ile eşleştirir. </item>
/// <item> Dış kapi ile ic kapilar arasındaki kamerayi eşleştirir. </item>
/// </list>
/// </summary>
public class SignalOuterDoor : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary> Dış kapı switch input kanalı (<c>IoChannel</c>). </summary>
    public Guid SwitchIoChannelId { get; set; }

    /// <summary> Switch kanalının "kapı açık" anlamına gelen değeri. </summary>
    public string SwitchOpenValue { get; set; } = "1";

    /// <summary> Dış kapıyı gören kamera. </summary>
    public Guid? CameraId { get; set; }

    /// <summary> Dış kapının aydınlatma LED'i (output <c>IoChannel</c>). </summary>
    public Guid? LightIoChannelId { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual SignalCabinet? Cabinet { get; set; }
    public virtual SignalOuterDoorState? State { get; set; }
    public virtual ICollection<SignalInnerDoor>? InnerDoors { get; set; }
    #endregion
}
