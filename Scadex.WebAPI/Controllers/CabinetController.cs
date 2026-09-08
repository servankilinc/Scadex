using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CabinetController : BaseController
{
    private readonly ICabinetService _cabinetService;
    public CabinetController(ILogger<CabinetController> logger, ICabinetService cabinetService) : base(logger)
    {
        _cabinetService = cabinetService;
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _cabinetService.GetDetailAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _cabinetService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _cabinetService.GetDetailAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _cabinetService.GetDetailListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _cabinetService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/detail")]
    public async Task<IActionResult> GetDetailList(DynamicRequest? request = default)
    {
        var result = await _cabinetService.GetDetailListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    public async Task<IActionResult> Create(CabinetCreateDto request)
    {
        var result = await _cabinetService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpGet("{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _cabinetService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(CabinetUpdateDto request)
    {
        var result = await _cabinetService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Selectlist
    [HttpGet("selectlist")]
    public async Task<IActionResult> SelectList()
    {
        var result = await _cabinetService.SelectListAsync();
        return ToAction(result);
    }
    #endregion

    #region Pagination / Datatable
    [HttpPost("pagination")]
    public async Task<IActionResult> Pagination(DynamicPaginationRequest request)
    {
        var result = await _cabinetService.PaginationAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/client")]
    public async Task<IActionResult> DatatableClientSide(DynamicDatatableRequest request)
    {
        var result = await _cabinetService.DatatableClientSideAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/server")]
    public async Task<IActionResult> DatatableServerSide(DynamicDatatableRequest request)
    {
        var result = await _cabinetService.DatatableServerSideAsync(request);
        return ToAction(result);
    }
    #endregion
}
