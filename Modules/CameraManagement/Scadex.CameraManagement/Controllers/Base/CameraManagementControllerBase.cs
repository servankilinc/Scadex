using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.RateLimiting;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.CameraManagement.Controllers.Base;

/// <summary>
/// Modül controller'ları WebAPI'deki <c>BaseController</c>'ı miras alamaz; aynı ProblemDetails sözleşmesini Core'un <c>GetProblemDetail</c>'i ile kurar.
/// Taban yalnızca kimlik ister; izin her aksiyonda ayrıca verilir (<c>[RequirePermission]</c>) — <c>[Authorize]</c>'lar VE ile birleşir.
/// Rate limit: çekirdeğin <see cref="RateLimiterKey.Default"/> politikası. Rota her controller'da ayrıca verilir: uçlar çekirdekteki
/// adreslerini korur (<c>api/Camera</c>, <c>api/CameraCaptureSetting</c>).
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting(RateLimiterKey.Default)]
public abstract class CameraManagementControllerBase : ControllerBase
{
    private readonly ILogger _logger;

    protected CameraManagementControllerBase(ILogger logger) => _logger = logger;

    protected IActionResult ToAction(Result result)
    {
        if (result.IsSuccess)
            return Ok();

        return ToProblem(result);
    }

    protected IActionResult ToAction<TData>(Result<TData> result)
    {
        if (result.IsSuccess)
            return Ok(result.Data);

        return ToProblem(result);
    }

    private ObjectResult ToProblem(IResult result)
    {
        if (result.Error != null)
            _logger.LogWarning("CameraManagement istegi basarisiz: {Message} {@Error}", result.Message, result.Error);

        var problemDetails = result.GetProblemDetail();
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
