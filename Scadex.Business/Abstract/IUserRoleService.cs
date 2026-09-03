using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;

namespace Scadex.Business.Abstract;

public interface IUserRoleService
{
    Task<Result<ICollection<string>>> GetRolesOfUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ICollection<SelectItemDto>>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<Result<bool>> IsInRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<Result> AssignAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
}
