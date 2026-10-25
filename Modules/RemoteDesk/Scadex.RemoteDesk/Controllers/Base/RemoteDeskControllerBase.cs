using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.RateLimiting;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.RemoteDesk.Controllers.Base;

/// <summary>
/// Taban yalnızca kimlik ister; izin policy'si her controller'da ayrıca verilir: izleme uçları <c>RemotePcView</c>,
/// rapor uçları <c>RemotePcAudit</c>. Policy tabanda OLMAMALI — ASP.NET <c>[Authorize]</c>'ları VE ile birleştirir, rapor da
/// izleme iznini isterdi. PC istemcisi <c>PcHub</c>'ı ve <c>PcFileTransferController</c>'ı kullanır — ikisi de kullanıcı JWT'si değil PC'nin
/// Basic Auth kimliğini ister (<c>DeviceAuthPolicies.Pc</c>).
/// <para/>
/// <b>Rate limit:</b> modülün HTTP uçları çekirdeğin <see cref="RateLimiterKey.Default"/> politikasındadır (kullanıcı başına; PC
/// uçlarında cihaz Id'si başına). Hub'lar bilerek dışarıdadır: <c>ViewerHub</c> <c>[DisableRateLimiting]</c> (WebSocket yoksa uzun yoklamada
/// her girdi paketi bir HTTP isteği olurdu), <c>PcHub</c> negotiate'i kendi IP politikasındadır (<c>RemoteDeskModule.PcHubRateLimitPolicy</c>).
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting(RateLimiterKey.Default)]
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
