using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.RemoteDesk.Controllers.Base;

/// <summary>
/// TODO: Modül enpointlerine rate limitting açılacak
/// <para/>Taban yalnızca kimlik ister; izin policy'si her controller'da ayrıca verilir: izleme uçları <c>RemotePcView</c>,
/// rapor uçları <c>RemotePcAudit</c>. Policy tabanda OLMAMALI — ASP.NET <c>[Authorize]</c>'ları VE ile birleştirir, rapor da
/// izleme iznini isterdi. PC istemcisi bu controller'ları değil <c>PcHub</c>'ı kullanır (anonim).
/// </summary>
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
