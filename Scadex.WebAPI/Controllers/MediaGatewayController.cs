using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.MediaGateway;
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
    private readonly IEnumerable<IMediaPathAuthorizer> _pathAuthorizers;

    public MediaGatewayController(ILogger<MediaGatewayController> logger, ICameraService cameraService, IEnumerable<IMediaPathAuthorizer> pathAuthorizers) : base(logger)
    {
        _cameraService = cameraService;
        _pathAuthorizers = pathAuthorizers;
    }

    /// <summary> <b>Cevap govdesizdir</b> ve <c>ToAction</c> kullanilmaz: MediaMTX yalnizca HTTP durum koduna bakar. </summary>
    [HttpPost("auth")]
    public async Task<IActionResult> Auth([FromBody] MediaMtxAuthDto request, CancellationToken cancellationToken)
    {
        // Modul yollari (orn. RemoteDesk pc_*): kamera yolu ASLA module sorulmaz, bir modul cam_ yolunu ustlenemez.
        string? path = request?.Path;
        if (request != null && !string.IsNullOrEmpty(path) && !IMediaGateway.IsManagedPathName(path))
        {
            var authorizer = _pathAuthorizers.FirstOrDefault(a => a.CanHandle(path));
            if (authorizer != null)
                return await authorizer.AuthorizeAsync(request, cancellationToken) ? Ok() : Unauthorized();
        }

        // KAMERA: YALNIZCA OKUMA (kameraya publish yoktur, kaynagi MediaMTX kendisi ceker).
        if (!string.Equals(request?.Action, "read", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        bool valid = await _cameraService.ValidateStreamTokenAsync(request?.Path, request?.Password, cancellationToken);

        return valid ? Ok() : Unauthorized();
    }
}
