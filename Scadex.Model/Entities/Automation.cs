using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class Automation : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public int? DeviceStatusId { get; set; }
    public DateTime? LastSeen { get; set; }
    // SCADA adresi / açık-kapalı / zaman aşımı / son ingest kontrol kartındadır (ControlCard, 2026-11-04).


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
    public virtual Company? Company { get; set; }
    public virtual DeviceStatus? DeviceStatus { get; set; }
    public virtual ICollection<Device>? Devices { get; set; }
    public virtual ICollection<DiagramAnnotation>? DiagramAnnotations { get; set; }
    public virtual ICollection<Connection>? Connections { get; set; }
    public virtual CanvasSettings? CanvasSettings { get; set; }
    public virtual ICollection<ChannelEvent>? ChannelEvents { get; set; }
    #endregion
}