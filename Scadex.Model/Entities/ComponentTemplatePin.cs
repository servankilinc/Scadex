using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

public class ComponentTemplatePin : IEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public int? ChannelNumber { get; set; }
    public PinFunction Function { get; set; }
    public PinDirection Direction { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }

    // ------------ Tasarım props ------------
    /// <summary>Sablonun genisligine gore 0..1 normalize kesir (CHECK ile kisitli).</summary>
    public double RelativeX { get; set; }
    /// <summary>Sablonun yuksekligine gore 0..1 normalize kesir (CHECK ile kisitli).</summary>
    public double RelativeY { get; set; }
    /// <summary>Pinin hangi kenarda durdugunu palet yazari burada bir kez belirler.</summary>
    public HandleSide Side { get; set; }
    // ------------ Tasarım props ------------

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual ComponentTemplate? ComponentTemplate { get; set; }
    public virtual ICollection<Pin>? Pins { get; set; }
    #endregion
}