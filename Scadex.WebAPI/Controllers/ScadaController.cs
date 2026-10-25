using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// SCADA'nın bize ulaşacağı endpointler. Kontrol kartı Basic Auth ile doğrulanır (kullanıcı adı = cihaz Id'si,);
/// kabin gövdeden değil kartın kimliğinden gelir. Kimliksiz / yanlış kimlikli istek 401.
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
        var result = await _channelEventService.IngestAsync(CabinetId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Kabindeki bir kart okuyucudan okunan kart </summary>
    [HttpPost("card")]
    public async Task<IActionResult> Card(ScadaCardReadRequest request, CancellationToken cancellationToken)
    {
        var result = await _cardReadService.IngestAsync(CabinetId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Policy cihaz kimliğini zorladığı için her zaman doludur. </summary>
    private Guid CabinetId => User.GetDeviceCabinetId() ?? throw new InvalidOperationException("Kontrol kartı kimliğinde kabin yok.");
}
