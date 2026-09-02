using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.DataAccess.Concrete;

public class RefreshTokenRepository : RepositoryBase<RefreshToken, AppDbContext>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context)
    {
    }

    public void RevokeDeviceRefreshTokens(Expression<Func<RefreshToken, bool>> where)
    {
        _context.RefreshTokens.Where(where).ExecuteUpdate(s => s.SetProperty(rt => rt.IsRevoked, true));
        SyncTrackedTokens(where);
    }

    public async Task RevokeDeviceRefreshTokensAsync(Expression<Func<RefreshToken, bool>> where, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.Where(where).ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true), cancellationToken);
        SyncTrackedTokens(where);
    }

    private void SyncTrackedTokens(Expression<Func<RefreshToken, bool>> where)
    {
        var predicate = where.Compile();
        foreach (var entry in _context.ChangeTracker.Entries<RefreshToken>())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Added)
                continue;
            if (!predicate(entry.Entity))
                continue;
            entry.Entity.IsRevoked = true;
            // Already persisted by ExecuteUpdate; do not re-write it on the next SaveChanges.
            entry.Property(nameof(RefreshToken.IsRevoked)).IsModified = false;
        }
    }
}