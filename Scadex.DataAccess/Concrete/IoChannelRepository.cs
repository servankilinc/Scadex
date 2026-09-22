using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class IoChannelRepository : RepositoryBase<IoChannel, AppDbContext>, IIoChannelRepository
{
    public IoChannelRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<bool> SetCurrentValueIfChangedAsync(Guid ioChannelId, string value, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        int affected = await _context.IoChannels
            .Where(c => c.Id == ioChannelId && (c.CurrentValue == null || c.CurrentValue != value))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.CurrentValue, value)
                .SetProperty(c => c.ValueUpdatedAt, updatedAtUtc),
                cancellationToken);

        if (affected == 0)
            return false;

        // ExecuteUpdate izlenen nesneyi güncellemez; aynı scope'ta izlenen bir kopya varsa sonraki SaveChanges eski değeri geri yazmasın.
        foreach (var entry in _context.ChangeTracker.Entries<IoChannel>().Where(e => e.Entity.Id == ioChannelId && e.State != EntityState.Detached))
        {
            entry.Entity.CurrentValue = value;
            entry.Entity.ValueUpdatedAt = updatedAtUtc;
            entry.Property(nameof(IoChannel.CurrentValue)).OriginalValue = value;
            entry.Property(nameof(IoChannel.ValueUpdatedAt)).OriginalValue = updatedAtUtc;
        }

        return true;
    }
}
