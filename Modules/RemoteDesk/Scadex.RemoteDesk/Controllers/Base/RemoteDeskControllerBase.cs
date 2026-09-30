using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.RemoteDesk.Controllers.Base;

/// <summary> TODO: Modül enpointlerine rate limitting açılacak </summary>
[ApiController]
[Authorize]
[Route("api/RemoteDesk")]
public abstract class RemoteDeskControllerBase : ControllerBase
{
    private readonly ILogger _logger;

    protected RemoteDeskControllerBase(ILogger logger) => _logger = logger;

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
            _logger.LogWarning("RemoteDesk istegi basarisiz: {Message} {@Error}", result.Message, result.Error);

        var problemDetails = result.GetProblemDetail();
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
