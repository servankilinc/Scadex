using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Connection.Queries;

public class ConnectionDto : IDto
{
    public Guid Id { get; set; }
    public Guid SourcePinId { get; set; }
    public Guid TargetPinId { get; set; }

    // ------------ Tasarım props ------------
    public string Label { get; set; } = null!;
    public WireType WireType { get; set; }
    public string Color { get; set; } = null!;
    public LineStyle LineStyle { get; set; }
    public double StrokeWidth { get; set; }
    public string WaypointsJson { get; set; } = null!;
    public int ZIndex { get; set; }
    // ------------ Tasarım props ------------

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
}