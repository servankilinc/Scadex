using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class RoleController : BaseController
{
    private readonly IRoleService _roleService;
    public RoleController(ILogger<RoleController> logger, IRoleService roleService) : base(logger)
    {
        _roleService = roleService;
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _roleService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _roleService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _roleService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _roleService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    public async Task<IActionResult> Create(RoleCreateDto request)
    {
        var result = await _roleService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpGet("{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _roleService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(RoleUpdateDto request)
    {
        var result = await _roleService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Selectlist
    [HttpGet("selectlist")]
    public async Task<IActionResult> SelectList()
    {
        var result = await _roleService.SelectListAsync();
        return ToAction(result);
    }
    #endregion

    #region Pagination / Datatable
    [HttpPost("pagination")]
    public async Task<IActionResult> Pagination(DynamicPaginationRequest request)
    {
        var result = await _roleService.PaginationAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/client")]
    public async Task<IActionResult> DatatableClientSide(DynamicDatatableRequest request)
    {
        var result = await _roleService.DatatableClientSideAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/server")]
    public async Task<IActionResult> DatatableServerSide(DynamicDatatableRequest request)
    {
        var result = await _roleService.DatatableServerSideAsync(request);
        return ToAction(result);
    }
    #endregion
}
