using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CanvasSettingsController : BaseController
{
    private readonly ICanvasSettingsService _canvasSettingsService;
    public CanvasSettingsController(ILogger<CanvasSettingsController> logger, ICanvasSettingsService canvasSettingsService) : base(logger) => 
        _canvasSettingsService = canvasSettingsService;
    

    [HttpPut("cabinet/{cabinetId:guid}")]
    public async Task<IActionResult> Upsert(Guid cabinetId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken)
    {
        var result = await _canvasSettingsService.UpsertAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _canvasSettingsService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _canvasSettingsService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _canvasSettingsService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _canvasSettingsService.GetBaseListAsync(request);
        return ToAction(result);
    } 
    #endregion
}
