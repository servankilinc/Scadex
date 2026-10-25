using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CompanyController : BaseController
{
    private readonly ICompanyService _companyService;
    public CompanyController(ILogger<CompanyController> logger, ICompanyService companyService) : base(logger) =>
        _companyService = companyService;


    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _companyService.GetBaseListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Create(CompanyCreateDto request)
    {
        var result = await _companyService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpPut]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(CompanyUpdateDto request)
    {
        var result = await _companyService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion
}
