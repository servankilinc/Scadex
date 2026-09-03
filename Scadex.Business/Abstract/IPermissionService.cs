using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.Permission.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IPermissionService
{
    // Get
    Task<Result<Permission>> GetAsync(Expression<Func<Permission, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Permission>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PermissionDto>> GetBaseAsync(int id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Permission>>> GetListAsync(Expression<Func<Permission, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Permission>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<PermissionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Permission, bool>>? where = default, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<PermissionDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<PermissionDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<PermissionDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}