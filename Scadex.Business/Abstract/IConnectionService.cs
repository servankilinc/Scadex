using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Connection.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IConnectionService
{
    // Get
    Task<Result<Connection>> GetAsync(Expression<Func<Connection, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Connection>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ConnectionDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Connection>>> GetListAsync(Expression<Func<Connection, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Connection>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ConnectionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}
