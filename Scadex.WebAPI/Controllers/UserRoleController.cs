using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class UserRoleController : BaseController
{
    private readonly IUserRoleService _userRoleService;
    public UserRoleController(ILogger<UserRoleController> logger, IUserRoleService userRoleService) : base(logger)
        => _userRoleService = userRoleService;


    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetRolesOfUser(Guid userId)
    {
        var result = await _userRoleService.GetRolesOfUserAsync(userId);
        return ToAction(result);
    }

    [HttpGet("role/{roleName}")]
    public async Task<IActionResult> GetUsersInRole(string roleName)
    {
        var result = await _userRoleService.GetUsersInRoleAsync(roleName);
        return ToAction(result);
    }

    [HttpGet("user/{userId:guid}/has/{roleName}")]
    public async Task<IActionResult> IsInRole(Guid userId, string roleName)
    {
        var result = await _userRoleService.IsInRoleAsync(userId, roleName);
        return ToAction(result);
    }

    [HttpPost("user/{userId:guid}/role/{roleName}")]
    public async Task<IActionResult> Assign(Guid userId, string roleName)
    {
        var result = await _userRoleService.AssignAsync(userId, roleName);
        return ToAction(result);
    }

    [HttpDelete("user/{userId:guid}/role/{roleName}")]
    public async Task<IActionResult> Remove(Guid userId, string roleName)
    {
        var result = await _userRoleService.RemoveAsync(userId, roleName);
        return ToAction(result);
    }
}
