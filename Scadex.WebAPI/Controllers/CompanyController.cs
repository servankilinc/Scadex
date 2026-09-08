using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CompanyController : BaseController
{
    private readonly ICompanyService _companyService;
    public CompanyController(ILogger<CompanyController> logger, ICompanyService companyService) : base(logger) => 
        _companyService = companyService;


    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _companyService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _companyService.GetBaseAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _companyService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _companyService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    public async Task<IActionResult> Create(CompanyCreateDto request)
    {
        var result = await _companyService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpGet("{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _companyService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(CompanyUpdateDto request)
    {
        var result = await _companyService.UpdateAsync(request);
        return ToAction(result);
    } 
    #endregion

    #region SelectList
    [HttpGet("selectlist")]
    public async Task<IActionResult> SelectList()
    {
        var result = await _companyService.SelectListAsync();
        return ToAction(result);
    }
    #endregion

    #region Pagination / Datatable
    [HttpPost("pagination")]
    public async Task<IActionResult> Pagination(DynamicPaginationRequest request)
    {
        var result = await _companyService.PaginationAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/client")]
    public async Task<IActionResult> DatatableClientSide(DynamicDatatableRequest request)
    {
        var result = await _companyService.DatatableClientSideAsync(request);
        return ToAction(result);
    }

    [HttpPost("datatable/server")]
    public async Task<IActionResult> DatatableServerSide(DynamicDatatableRequest request)
    {
        var result = await _companyService.DatatableServerSideAsync(request);
        return ToAction(result);
    } 
    #endregion
}
