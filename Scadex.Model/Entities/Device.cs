using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class Device : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public int? DeviceStatusId { get; set; }
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

    #region *** EF Core Navigation ***
    public virtual Cabinet? Cabinet { get; set; }
    public virtual ComponentTemplate? ComponentTemplate { get; set; }
    public virtual DeviceStatus? DeviceStatus { get; set; }
    public virtual ICollection<IoChannel>? IoChannels { get; set; }
    public virtual ICollection<Pin>? Pins { get; set; }
    public virtual ICollection<DeviceCommand>? DeviceCommands { get; set; }
    #endregion
}