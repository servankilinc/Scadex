using Scadex.Core.Model;

namespace Scadex.Model.Dtos.DeviceStatus.Queries;

public class DeviceStatusDto : IDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public string Icon { get; set; } = null!;
    public string? Description { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion
}