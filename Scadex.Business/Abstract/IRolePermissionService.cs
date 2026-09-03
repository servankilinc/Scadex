using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.RolePermission.Commands;
using Scadex.Model.Dtos.RolePermission.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IRolePermissionService
{
    // Get
    Task<Result<RolePermission>> GetAsync(Expression<Func<RolePermission, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<RolePermission>> GetAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default);
    Task<Result<RolePermissionDto>> GetBaseAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<RolePermission>>> GetListAsync(Expression<Func<RolePermission, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<RolePermission>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<RolePermissionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    /// <summary>Bir rolun sahip oldugu tum izinleri, Permission bilgileriyle birlikte getirir.</summary>
    Task<Result<ICollection<RolePermissionDto>>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(RolePermissionCreateDto request, CancellationToken cancellationToken = default);

    // Delete
    Task<Result> DeleteAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default);

    // Sync
    /// <summary>Rolun izin kumesini verilen liste ile birebir degistirir (ekle + sil), tek transaction icinde.</summary>
    Task<Result> SyncRolePermissionsAsync(Guid roleId, ICollection<int> permissionIds, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<RolePermissionDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<RolePermissionDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<RolePermissionDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}
