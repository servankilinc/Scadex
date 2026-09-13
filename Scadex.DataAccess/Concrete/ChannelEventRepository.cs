using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.Pagination;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class ChannelEventRepository : RepositoryBase<ChannelEvent, AppDbContext>, IChannelEventRepository
{
    public ChannelEventRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PaginationResponse<ChannelEventDto>> GetPagedAsync(
        IConfigurationProvider configurationProvider,
        Guid cabinetId,
        Guid? ioChannelId,
        DateTime? fromUtc,
        DateTime? toUtc,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ChannelEvents.AsNoTracking().Where(e => e.CabinetId == cabinetId);

        if (ioChannelId.HasValue)
            query = query.Where(e => e.IoChannelId == ioChannelId.Value);

        if (fromUtc.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= toUtc.Value);

        return await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .ProjectTo<ChannelEventDto>(configurationProvider)
            .ToPaginateAsync(pagination, cancellationToken);
    }
}
