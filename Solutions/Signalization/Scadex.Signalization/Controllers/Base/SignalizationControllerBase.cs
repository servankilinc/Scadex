using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.RateLimiting;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Signalization.Controllers.Base;

/// <summary>
/// Modül controller'larının tabanı: <c>BaseController</c> WebAPI'de olduğu için miras alınamaz; aynı ProblemDetails sözleşmesini Core'un
/// <c>GetProblemDetail</c>'i ile kurar. Rate limit: çekirdeğin <see cref="RateLimiterKey.Default"/> politikası (kullanıcı başına);
/// <c>SignalizationHub</c> bilerek dışarıdadır.
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting(RateLimiterKey.Default)]
[Route("api/[controller]")]
public abstract class SignalizationControllerBase : ControllerBase
{
    private readonly ILogger _logger;

    protected SignalizationControllerBase(ILogger logger) => _logger = logger;

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
            _logger.LogWarning("Sinyalizasyon istegi basarisiz: {Message} {@Error}", result.Message, result.Error);

        var problemDetails = result.GetProblemDetail();
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
