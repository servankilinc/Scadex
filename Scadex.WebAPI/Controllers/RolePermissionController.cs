using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.RolePermission.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class RolePermissionController : BaseController
{
    private readonly IRolePermissionService _rolePermissionService;
    public RolePermissionController(ILogger<RolePermissionController> logger, IRolePermissionService rolePermissionService) : base(logger)
    {
        _rolePermissionService = rolePermissionService;
    }

    #region Get
    [HttpGet("role/{roleId:guid}")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> GetByRole(Guid roleId)
    {
        var result = await _rolePermissionService.GetByRoleAsync(roleId);
        return ToAction(result);
    }

    /// <summary> Yetki matrisi: her rol ve izin kimlikleri. Matris ekranı ve kullanıcı rol dialogundaki "etkin izinler" önizlemesi okur. </summary>
    [HttpGet("matrix")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> GetMatrix(CancellationToken cancellationToken)
    {
        var result = await _rolePermissionService.GetMatrixAsync(cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Sync
    /// <summary>Rolun izin kumesini gonderilen liste ile birebir degistirir. Rol dialogundaki "İzinler" bunu kullanir.</summary>
    [HttpPut("role/{roleId:guid}/sync")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Sync(Guid roleId, [FromBody] ICollection<int> permissionIds)
    {
        var result = await _rolePermissionService.SyncRolePermissionsAsync(roleId, permissionIds);
        return ToAction(result);
    }

    /// <summary> Gönderilen rollerin izin kümelerini tek transaction'da eşitler; gönderilmeyen rollere dokunmaz. </summary>
    [HttpPut("matrix")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> SaveMatrix([FromBody] ICollection<RolePermissionMatrixSaveItem> items, CancellationToken cancellationToken)
    {
        var result = await _rolePermissionService.SaveMatrixAsync(items, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
