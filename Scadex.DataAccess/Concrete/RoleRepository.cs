using Microsoft.EntityFrameworkCore;
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

    public async Task<Dictionary<Guid, int>> GetUserCountsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.RoleId, row => row.Count, cancellationToken);
    }
}
