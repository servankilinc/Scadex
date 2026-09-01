using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

public class ComponentTemplateDetailDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public bool IsSystemTemplate { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string? BackgroundImageUrl { get; set; }
    public string DeviceTypeName { get; set; } = null!;

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; }
    #endregion
}