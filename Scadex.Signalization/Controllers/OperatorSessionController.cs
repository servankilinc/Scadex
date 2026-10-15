using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.Auth;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Session.Commands;
using Scadex.Signalization.Model.Dtos.Session.Queries;
using Scadex.Signalization.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Controllers;

/// <summary> Operator islemleri: canli panel ve rapor (okuma) + yetkili kullanicinin elle kapatmasi. </summary>
public class OperatorSessionController : SignalizationControllerBase
{
    private readonly IOperatorSessionService _service;
    public OperatorSessionController(ILogger<OperatorSessionController> logger, IOperatorSessionService service) : base(logger) => _service = service;


    [HttpGet("open")]
    public async Task<IActionResult> GetOpen([FromQuery] Guid? cabinetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetOpenAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("list")]
    public async Task<IActionResult> List(OperatorSessionQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(request, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetDetailAsync(id, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("summary")]
    public async Task<IActionResult> Summary(OperatorSessionSummaryRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.GetSummaryAsync(request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Açık işlemi elle kapatır; gerekçe zorunlu. Yalnızca <c>SignalSessionClose</c> iznine açık (yoksa 403). </summary>
    [HttpPost("{id:long}/close")]
    [Authorize(Policy = SignalizationModule.SessionClosePolicy)]
    public async Task<IActionResult> Close(long id, OperatorSessionCloseRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var result = await _service.CloseAsync(id, userId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Kabinin son kart okumalari — sanal kabinin kart okuyucusu (sanal kabin izni). </summary>
    [HttpGet("cabinet/{cabinetId:guid}/card-reads")]
    [RequirePermission(Permission.SignalVirtualCabinet)]
    public async Task<IActionResult> GetRecentCardReads(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetRecentCardReadsAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }
}
