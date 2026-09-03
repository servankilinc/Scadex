using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.IoChannel.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IIoChannelService
{
    // Get
    Task<Result<IoChannel>> GetAsync(Expression<Func<IoChannel, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<IoChannel>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IoChannelDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<IoChannel>>> GetListAsync(Expression<Func<IoChannel, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<IoChannel>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<IoChannelDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}
