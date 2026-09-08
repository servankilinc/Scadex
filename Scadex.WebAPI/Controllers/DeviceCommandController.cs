using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class DeviceCommandController : BaseController
{
    private readonly IDeviceCommandService _deviceCommandService;
    public DeviceCommandController(ILogger<DeviceCommandController> logger, IDeviceCommandService deviceCommandService) : base(logger)
    {
        _deviceCommandService = deviceCommandService;
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _deviceCommandService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _deviceCommandService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _deviceCommandService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _deviceCommandService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    public async Task<IActionResult> Create(DeviceCommandCreateDto request)
    {
        var result = await _deviceCommandService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpGet("{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _deviceCommandService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(DeviceCommandUpdateDto request)
    {
        var result = await _deviceCommandService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Delete / Restore
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _deviceCommandService.DeleteAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var result = await _deviceCommandService.RestoreAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region SelectList
    [HttpGet("selectlist")]
    public async Task<IActionResult> SelectList()
    {
        var result = await _deviceCommandService.SelectListAsync();
        return ToAction(result);
    }
    #endregion

    #region Pagination / Datatable
    [HttpPost("pagination")]
    public async Task<IActionResult> Pagination(DynamicPaginationRequest request)
    {
        var result = await _deviceCommandService.PaginationAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/client")]
    public async Task<IActionResult> DatatableClientSide(DynamicDatatableRequest request)
    {
        var result = await _deviceCommandService.DatatableClientSideAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/server")]
    public async Task<IActionResult> DatatableServerSide(DynamicDatatableRequest request)
    {
        var result = await _deviceCommandService.DatatableServerSideAsync(request);
        return ToAction(result);
    }
    #endregion
}
