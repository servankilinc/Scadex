using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.MediaGatewaySetting.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class MediaGatewaySettingController : BaseController
{
    private readonly IMediaGatewaySettingService _settingService;

    public MediaGatewaySettingController(ILogger<MediaGatewaySettingController> logger, IMediaGatewaySettingService settingService) : base(logger)
    {
        _settingService = settingService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _settingService.GetAsync(cancellationToken);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(MediaGatewaySettingUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _settingService.UpdateAsync(request, cancellationToken);
        return ToAction(result);
    }
}
