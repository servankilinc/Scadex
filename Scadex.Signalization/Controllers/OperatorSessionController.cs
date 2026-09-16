using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Session.Queries;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Controllers;

/// <summary> Operator islemleri: canli panel ve rapor (salt okunur). </summary>
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
}
