using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class DeviceStatus : IEntity, IAuditableEntity, IImmutableEntity
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

    #region *** EF Core Navigation ***
    public virtual ICollection<Cabinet>? Cabinets { get; set; }
    public virtual ICollection<Device>? Devices { get; set; }
    public virtual ICollection<Camera>? Cameras { get; set; }
    #endregion
}