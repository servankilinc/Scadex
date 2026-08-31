using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

public class Pin : IEntity, ISoftDeletableEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid? ComponentTemplatePinId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    public int? ChannelNumber { get; set; }
    public string Name { get; set; } = null!;
    public PinFunction Function { get; set; }
    public PinDirection Direction { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }

    // ------------ Tasarım props ------------
    /// <summary>Sablonun genisligine gore 0..1 normalize kesir (CHECK ile kisitli).</summary>
    public double RelativeX { get; set; }
    /// <summary>Sablonun yuksekligine gore 0..1 normalize kesir (CHECK ile kisitli).</summary>
    public double RelativeY { get; set; }
    /// <summary>React Flow Handle position karsiligi.</summary>
    public HandleSide Side { get; set; }
    // ------------ Tasarım props ------------

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    public virtual IoChannel? IoChannel { get; set; }
    public virtual ComponentTemplatePin? ComponentTemplatePin { get; set; }
    public virtual ICollection<Connection>? SourcePinConnections { get; set; }
    public virtual ICollection<Connection>? TargetPinConnections { get; set; }
    #endregion
}