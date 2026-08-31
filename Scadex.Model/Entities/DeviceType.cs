using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class DeviceType : IEntity, IAuditableEntity, IImmutableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual ICollection<ComponentTemplate>? ComponentTemplates { get; set; }
    #endregion
}