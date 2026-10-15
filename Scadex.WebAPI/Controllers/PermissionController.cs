using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

/// <summary> İzin listesi (seed'le gelir, salt okunur) — rol izin matrisi ekranı okur. </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class PermissionController : BaseController
{
    private readonly IPermissionService _permissionService;
    public PermissionController(ILogger<PermissionController> logger, IPermissionService permissionService) : base(logger)
    {
        _permissionService = permissionService;
    }

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _permissionService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion
}
