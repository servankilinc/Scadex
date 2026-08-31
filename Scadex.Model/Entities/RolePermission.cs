using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class RolePermission : IEntity
{
    public Guid RoleId { get; set; }
    public int PermissionId { get; set; }
    
    #region *** EF Core Navigation ***
    public virtual Role? Role { get; set; }
    public virtual Permission? Permission { get; set; } 
    #endregion
}