using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Model.Dtos.MediaGateway;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;

namespace Scadex.WebAPI.Controllers;

/// <summary> Sadece Medya Gateway (MediaMTX) kimlik tarafından kullanılan bizim endponitlerimiz. </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimiterKey.MediaGateway)]
public class MediaGatewayController : BaseController
{
    private readonly IEnumerable<IMediaPathAuthorizer> _pathAuthorizers;
    private readonly ILogger<MediaGatewayController> _logger;

    public MediaGatewayController(ILogger<MediaGatewayController> logger, IEnumerable<IMediaPathAuthorizer> pathAuthorizers) : base(logger)
    {
        _pathAuthorizers = pathAuthorizers;
        _logger = logger;
    }

    /// <summary>
    /// <b>Cevap govdesizdir</b> ve <c>ToAction</c> kullanilmaz: MediaMTX yalnizca HTTP durum koduna bakar.
    /// Karari yolu ustlenen TEK yetkilendirici verir (kamera <c>cam_</c>, RemoteDesk <c>pc_</c>); sahipsiz ya da iki sahipli yol 401'dir.
    /// </summary>
    [HttpPost("auth")]
    public async Task<IActionResult> Auth([FromBody] MediaMtxAuthDto request, CancellationToken cancellationToken)
    {
        string? path = request?.Path;
        if (request == null || string.IsNullOrEmpty(path))
            return Unauthorized();

        var owners = _pathAuthorizers.Where(a => a.CanHandle(path)).Take(2).ToList();
        if (owners.Count != 1)
        {
            if (owners.Count > 1)
                _logger.LogError("Medya yolu {Path} birden fazla yetkilendirici tarafindan ustlenildi; reddedildi.", path);
            return Unauthorized();
        }

        return await owners[0].AuthorizeAsync(request, cancellationToken) ? Ok() : Unauthorized();
    }
}
