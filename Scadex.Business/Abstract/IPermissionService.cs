using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Permission.Queries;

namespace Scadex.Business.Abstract;

public interface IPermissionService
{
    // List
    Task<Result<ICollection<PermissionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}