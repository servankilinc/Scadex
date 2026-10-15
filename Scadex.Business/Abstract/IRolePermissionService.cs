using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.RolePermission.Queries;

namespace Scadex.Business.Abstract;

public interface IRolePermissionService
{
    /// <summary>Bir rolun sahip oldugu tum izinleri, Permission bilgileriyle birlikte getirir.</summary>
    Task<Result<ICollection<RolePermissionDto>>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    // Sync
    /// <summary>Rolün izin kümesini verilen liste ile birebir degistirir (ekle + sil), tek transaction icinde.</summary>
    Task<Result> SyncRolePermissionsAsync(Guid roleId, ICollection<int> permissionIds, CancellationToken cancellationToken = default);
}
