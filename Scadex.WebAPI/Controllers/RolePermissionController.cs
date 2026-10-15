using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
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
    public async Task<IActionResult> GetByRole(Guid roleId)
    {
        var result = await _rolePermissionService.GetByRoleAsync(roleId);
        return ToAction(result);
    }
    #endregion

    /// <summary>Rolun izin kumesini gonderilen liste ile birebir degistirir. Izin matrisi ekrani bunu kullanir.</summary>
    [HttpPut("role/{roleId:guid}/sync")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Sync(Guid roleId, [FromBody] ICollection<int> permissionIds)
    {
        var result = await _rolePermissionService.SyncRolePermissionsAsync(roleId, permissionIds);
        return ToAction(result);
    }
}