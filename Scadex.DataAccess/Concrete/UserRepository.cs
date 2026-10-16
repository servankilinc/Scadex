using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class UserRepository : RepositoryBase<User, AppDbContext>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Dictionary<Guid, List<string>>> GetRoleNamesByUserAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.Id
            select new { userRole.UserId, role.Name }
        ).AsNoTracking().ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Name!).OrderBy(name => name).ToList());
    }

    public async Task<List<User>> GetUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await (
            from userRole in _context.UserRoles
            join user in _context.Users on userRole.UserId equals user.Id
            where userRole.RoleId == roleId
            orderby user.FullName
            select user
        ).AsNoTracking().ToListAsync(cancellationToken);
    }
}
