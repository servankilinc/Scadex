using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

/// <summary> SCADA'nın bize ulaşacağı endpointler. </summary>
[AllowAnonymous]
[EnableRateLimiting(RateLimiterKey.Scada)]
public class ScadaController : BaseController
{
    private readonly IChannelEventService _channelEventService;

    public ScadaController(ILogger<ScadaController> logger, IChannelEventService channelEventService) : base(logger)
    {
        _channelEventService = channelEventService;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(ScadaIngestRequest request, CancellationToken cancellationToken)
    {
        var result = await _channelEventService.IngestAsync(request, cancellationToken);
        return ToAction(result);
    }
}
