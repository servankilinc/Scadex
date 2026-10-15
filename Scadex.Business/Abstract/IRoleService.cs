using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.Model.Dtos.Role.Queries;

namespace Scadex.Business.Abstract;

public interface IRoleService
{
    // List
    Task<Result<ICollection<RoleDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(RoleCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result> UpdateAsync(RoleUpdateDto request, CancellationToken cancellationToken = default);
}