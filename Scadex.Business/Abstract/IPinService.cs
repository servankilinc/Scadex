using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Pin.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IPinService
{
    // Get
    Task<Result<Pin>> GetAsync(Expression<Func<Pin, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Pin>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PinDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PinDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Pin>>> GetListAsync(Expression<Func<Pin, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Pin>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<PinDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<PinDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}
