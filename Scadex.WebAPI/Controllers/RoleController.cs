using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class RoleController : BaseController
{
    private readonly IRoleService _roleService;
    public RoleController(ILogger<RoleController> logger, IRoleService roleService) : base(logger)
    {
        _roleService = roleService;
    }

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _roleService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Create(RoleCreateDto request)
    {
        var result = await _roleService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpPut]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Update(RoleUpdateDto request)
    {
        var result = await _roleService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion
}
