using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class RolePermissionRepository : RepositoryBase<RolePermission, AppDbContext>, IRolePermissionRepository
{
    public RolePermissionRepository(AppDbContext context) : base(context)
    {
    }
}