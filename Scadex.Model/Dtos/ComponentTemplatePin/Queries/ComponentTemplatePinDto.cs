using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.ComponentTemplatePin.Queries;

public class ComponentTemplatePinDto : IDto
{
    public Guid Id { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public int? ChannelNumber { get; set; }
    public PinFunction Function { get; set; }
    public PinDirection Direction { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }

    // ------------ Tasarım props ------------
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public HandleSide Side { get; set; }
    // ------------ Tasarım props ------------

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; } 
    #endregion
}