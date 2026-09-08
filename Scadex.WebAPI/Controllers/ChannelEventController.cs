using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class ChannelEventController : BaseController
{
    private readonly IChannelEventService _channelEventService;

    public ChannelEventController(ILogger<ChannelEventController> logger, IChannelEventService channelEventService) : base(logger)
    {
        _channelEventService = channelEventService;
    }

    [HttpPost("list")]
    public async Task<IActionResult> List(ChannelEventQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _channelEventService.GetPagedAsync(request, cancellationToken);
        return ToAction(result);
    }
}
