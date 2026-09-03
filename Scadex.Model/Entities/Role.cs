using Scadex.Core.Model;
using Microsoft.AspNetCore.Identity;

namespace Scadex.Model.Entities;

public class Role : IdentityRole<Guid>, IEntity, IAuditableEntity, IActivatableEntity
{
    //public Guid Id { get; set; }
    //public string Name { get; set; } = null!;
    public bool IsImmutable { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; } = true;
    #endregion

    #region *** EF Core Navigation ***
    public virtual ICollection<RolePermission>? RolePermissions { get; set; }
    #endregion
}