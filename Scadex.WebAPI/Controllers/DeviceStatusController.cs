using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class DeviceStatusController : BaseController
{
    private readonly IDeviceStatusService _deviceStatusService;
    public DeviceStatusController(ILogger<DeviceStatusController> logger, IDeviceStatusService deviceStatusService) : base(logger)
        => _deviceStatusService = deviceStatusService;


    #region Get
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _deviceStatusService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:int}/base")]
    public async Task<IActionResult> GetBase(int id)
    {
        var result = await _deviceStatusService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _deviceStatusService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _deviceStatusService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Selectlist
    [HttpGet("selectlist")]
    public async Task<IActionResult> SelectList()
    {
        var result = await _deviceStatusService.SelectListAsync();
        return ToAction(result);
    }
    #endregion

    #region Pagination / Datatable
    [HttpPost("pagination")]
    public async Task<IActionResult> Pagination(DynamicPaginationRequest request)
    {
        var result = await _deviceStatusService.PaginationAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/client")]
    public async Task<IActionResult> DatatableClientSide(DynamicDatatableRequest request)
    {
        var result = await _deviceStatusService.DatatableClientSideAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/server")]
    public async Task<IActionResult> DatatableServerSide(DynamicDatatableRequest request)
    {
        var result = await _deviceStatusService.DatatableServerSideAsync(request);
        return ToAction(result);
    }
    #endregion
}
