using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.Auth;
using Scadex.CameraManagement.Controllers.Base;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Commands;
using Scadex.CameraManagement.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.CameraManagement.Controllers;

[Route("api/CameraCaptureSetting")]
public class CameraCaptureSettingController : CameraManagementControllerBase
{
    private readonly ICameraCaptureSettingService _settingService;

    public CameraCaptureSettingController(ILogger<CameraCaptureSettingController> logger, ICameraCaptureSettingService settingService) : base(logger)
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
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(CameraCaptureSettingUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _settingService.UpdateAsync(request, cancellationToken);
        return ToAction(result);
    }
}
