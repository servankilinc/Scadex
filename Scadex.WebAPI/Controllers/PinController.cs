using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class PinController : BaseController
{
    private readonly IPinService _pinService;
    public PinController(ILogger<PinController> logger, IPinService pinService) : base(logger)
    {
        _pinService = pinService;
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _pinService.GetDetailAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _pinService.GetDetailAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _pinService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _pinService.GetDetailListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/detail")]
    public async Task<IActionResult> GetDetailList(DynamicRequest? request = default)
    {
        var result = await _pinService.GetDetailListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _pinService.GetBaseListAsync(request);
        return ToAction(result);
    } 
    #endregion
}
