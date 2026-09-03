using AutoMapper;
using Scadex.Core.Utils.Pagination;
using Scadex.DataAccess.Repository;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IChannelEventRepository : IRepository<ChannelEvent>, IRepositoryAsync<ChannelEvent>
{
    /// <summary> Bir kabinin olaylari — yeniden eskiye, sayfali. </summary>
    Task<PaginationResponse<ChannelEventDto>> GetPagedAsync(
        IConfigurationProvider configurationProvider,
        Guid cabinetId,
        Guid? ioChannelId,
        DateTime? fromUtc,
        DateTime? toUtc,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);
}
