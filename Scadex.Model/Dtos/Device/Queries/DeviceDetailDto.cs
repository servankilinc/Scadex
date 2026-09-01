using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Device.Queries;

public class DeviceDetailDto : IDto
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string ComponentTemplateName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int? DeviceStatusId { get; set; }
    public string? DeviceStatusName { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? ExternalCode { get; set; }
    public DateTime? LastSeen { get; set; }
    
    // ------------ Tasarım props ------------
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; }
    // ------------ Tasarım props ------------

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