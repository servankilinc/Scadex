using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// Kameranın CİHAZ bilgisi (çekirdek). İzleme bileti, anlık görüntü ve kare/klip uçları aynı <c>api/Camera</c> öneki altında
/// CameraManagement modülündedir (<c>CameraMediaController</c>); modül kapalıyken yalnızca bu uçlar kalır.
/// </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class CameraController : BaseController
{
    private readonly ICameraService _cameraService;

    public CameraController(ILogger<CameraController> logger, ICameraService cameraService) : base(logger)
    {
        _cameraService = cameraService;
    }

    /// <summary>Bir otomasyondaki kameralar.</summary>
    [HttpGet("automation/{automationId:guid}")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> ListByAutomation(Guid automationId, [FromQuery] bool includePassive, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetListAsync(automationId, includePassive, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetAsync(id, cancellationToken);
        return ToAction(result);
    }

    [HttpPut]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(CameraUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraService.UpdateAsync(request, cancellationToken);
        return ToAction(result);
    }
}
