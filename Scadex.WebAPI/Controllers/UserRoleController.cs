using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class UserRoleController : BaseController
{
    private readonly IUserRoleService _userRoleService;
    public UserRoleController(ILogger<UserRoleController> logger, IUserRoleService userRoleService) : base(logger)
        => _userRoleService = userRoleService;


    [HttpGet("user/{userId:guid}")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> GetRolesOfUser(Guid userId)
    {
        var result = await _userRoleService.GetRolesOfUserAsync(userId);
        return ToAction(result);
    }

    /// <summary> Kullanıcının rollerini tek istekte eşitler (gövde rol adları). </summary>
    [HttpPut("user/{userId:guid}/sync")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Sync(Guid userId, [FromBody] ICollection<string> roleNames, CancellationToken cancellationToken)
    {
        var result = await _userRoleService.SyncAsync(userId, roleNames, cancellationToken);
        return ToAction(result);
    }

    #region Role members
    [HttpGet("role/{roleId:guid}/users")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> GetMembers(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await _userRoleService.GetMembersAsync(roleId, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("role/{roleId:guid}/users/{userId:guid}")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> AddMember(Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await _userRoleService.AddMemberAsync(roleId, userId, cancellationToken);
        return ToAction(result);
    }

    [HttpDelete("role/{roleId:guid}/users/{userId:guid}")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> RemoveMember(Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await _userRoleService.RemoveMemberAsync(roleId, userId, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
