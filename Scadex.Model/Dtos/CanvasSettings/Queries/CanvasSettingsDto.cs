using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.CanvasSettings.Queries;

public class CanvasSettingsDto : IDto
{
    public Guid Id { get; set; }
    public int GridSize { get; set; }
    public bool SnapToGrid { get; set; }
    public BackgroundVariant BackgroundVariant { get; set; }
    public string GridColor { get; set; } = null!;
    public string BackgroundColor { get; set; } = null!;
    public double MinZoom { get; set; }
    public double MaxZoom { get; set; }
    
    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; } 
    #endregion
}