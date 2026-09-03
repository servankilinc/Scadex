using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.Model.Dtos.Role.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IRoleService
{
    // Get
    Task<Result<Role>> GetAsync(Expression<Func<Role, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Role>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<RoleDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Role>>> GetListAsync(Expression<Func<Role, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Role>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<RoleDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Role, bool>>? where = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(RoleCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<RoleUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(RoleUpdateDto request, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<RoleDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<RoleDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<RoleDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}