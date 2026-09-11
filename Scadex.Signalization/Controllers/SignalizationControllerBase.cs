using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Signalization.Controllers;

[ApiController]
[Authorize]
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

    private ObjectResult ToProblem(Scadex.Core.Utils.ResultPattern.IResult result)
    {
        if (result.Error != null)
            _logger.LogWarning("Sinyalizasyon istegi basarisiz: {Message} {@Error}", result.Message, result.Error);

        var problemDetails = result.GetProblemDetail();
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
