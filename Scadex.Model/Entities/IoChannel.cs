using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

public class IoChannel : IEntity, ISoftDeletableEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public int ChannelNumber { get; set; }
    public PinDirection Direction { get; set; }
    public bool IsEnabled { get; set; }
    public string? CurrentValue { get; set; }
    public string Name { get; set; } = null!;
    public DateTime? ValueUpdatedAt { get; set; }

    /// <summary>
    /// Bu kanalın değer değişimleri <see cref="ChannelEvent"/> olarak kalıcı kaydedilsin mi? <para/>
    /// Bayrak açık olsa bile  yalnızca <see cref="PinDirection.Input"/> kanallar  <see cref="ChannelEvent"/>  üretir
    /// </summary>
    public bool IsEventLogged { get; set; }

    /// <summary> Doluysa olay YALNIZCA bu değere geçişte yazılır; <c>null</c> ise her değişim olaydır. </summary>
    public string? EventTriggerValue { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- ISoftDeletableEntity ---
    public string? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    public virtual ICollection<Pin>? Pins { get; set; }
    public virtual ICollection<DeviceCommand>? DeviceCommands { get; set; }
    public virtual ICollection<ChannelEvent>? ChannelEvents { get; set; }
    #endregion
}