using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

/// <summary> Sadece Medya Gateway (MediaMTX) kimlik tarafından kullanılan bizim endponitlerimiz. </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimiterKey.MediaGateway)]
public class MediaGatewayController : BaseController
{
    private readonly ICameraService _cameraService;

    public MediaGatewayController(ILogger<MediaGatewayController> logger, ICameraService cameraService) : base(logger)
    {
        _cameraService = cameraService;
    }

    /// <summary> <b>Cevap govdesizdir</b> ve <c>ToAction</c> kullanilmaz: MediaMTX yalnizca HTTP durum koduna bakar. </summary>
    [HttpPost("auth")]
    public async Task<IActionResult> Auth([FromBody] MediaMtxAuthDto request, CancellationToken cancellationToken)
    {
        // YALNIZCA OKUMA. 
        if (!string.Equals(request?.Action, "read", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        bool valid = await _cameraService.ValidateStreamTokenAsync(request?.Path, request?.Password, cancellationToken);

        return valid ? Ok() : Unauthorized();
    }
}
