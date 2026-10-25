using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary> Tuval ayarlarının tek yazım yolu. Okuma diyagramla birlikte gelir (<c>GET /api/Diagram/cabinet/{id}</c>). </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class CanvasSettingsController : BaseController
{
    private readonly ICanvasSettingsService _canvasSettingsService;
    public CanvasSettingsController(ILogger<CanvasSettingsController> logger, ICanvasSettingsService canvasSettingsService) : base(logger) =>
        _canvasSettingsService = canvasSettingsService;


    [HttpPut("cabinet/{cabinetId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> Upsert(Guid cabinetId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken)
    {
        var result = await _canvasSettingsService.UpsertAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }
}
