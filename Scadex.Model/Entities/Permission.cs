using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class Permission : IEntity, IAuditableEntity, IImmutableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Category { get; set; } = null!;

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual ICollection<RolePermission>? RolePermissions { get; set; }
    #endregion
}