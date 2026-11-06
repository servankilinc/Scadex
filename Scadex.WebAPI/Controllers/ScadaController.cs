using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using Scadex.Model.Scada.Commands;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// SCADA'nın bize ulaşacağı endpointler (Docs/scada-http-api.md). Kontrol kartı <c>DeviceId</c> header'ındaki MAC adresiyle tanınır;
/// otomasyon gövdeden değil kartın kimliğinden gelir. Header'sız / eşleşmeyen istek 401.
/// </summary>
[Authorize(Policy = DeviceAuthPolicies.ControlCard)]
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
        var result = await _channelEventService.IngestAsync(AutomationId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Otomasyondaki bir kart okuyucudan okunan kart </summary>
    [HttpPost("card")]
    public async Task<IActionResult> Card(ScadaCardReadRequest request, CancellationToken cancellationToken)
    {
        var result = await _cardReadService.IngestAsync(AutomationId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Policy cihaz kimliğini zorladığı için her zaman doludur. </summary>
    private Guid AutomationId => User.GetDeviceAutomationId() ?? throw new InvalidOperationException("Kontrol kartı kimliğinde otomasyon yok.");
}
