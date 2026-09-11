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
    private readonly ICardReadService _cardReadService;

    public ScadaController(ILogger<ScadaController> logger, IChannelEventService channelEventService, ICardReadService cardReadService) : base(logger)
    {
        _channelEventService = channelEventService;
        _cardReadService = cardReadService;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(ScadaIngestRequest request, CancellationToken cancellationToken)
    {
        var result = await _channelEventService.IngestAsync(request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Kabindeki bir kart okuyucudan okunan kart </summary>
    [HttpPost("card")]
    public async Task<IActionResult> Card(ScadaCardReadRequest request, CancellationToken cancellationToken)
    {
        var result = await _cardReadService.IngestAsync(request, cancellationToken);
        return ToAction(result);
    }
}
