using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class RoleRepository : RepositoryBase<Role, AppDbContext>, IRoleRepository
{
    public RoleRepository(AppDbContext context) : base(context)
    {
    }
}