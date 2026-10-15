using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary> Cihaza komut ve komut geçmişi. Cihazın kendisi diyagramla okunur/yazılır (<c>/api/Diagram</c>). </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class DeviceController : BaseController
{
    private readonly IDeviceCommandService _deviceCommandService;

    public DeviceController(ILogger<DeviceController> logger, IDeviceCommandService deviceCommandService) : base(logger)
    {
        _deviceCommandService = deviceCommandService;
    }

    #region Komut İşlemleri
    /// <summary>
    /// Diyagramdan komut: sahaya fiziksel etki eder (röle, kilit, siren). İki izin VE ile birleşir — komut göndermek için
    /// diyagramı izleme izni de gerekir (görmeden komut yok).
    /// </summary>
    [HttpPost("{deviceId:guid}/command")]
    [RequirePermission(Permission.ViewDiagram)]
    [RequirePermission(Permission.ControlOutput)]
    public async Task<IActionResult> SendCommand(Guid deviceId, DeviceCommandSendRequest request, CancellationToken cancellationToken)
    {
        var result = await _deviceCommandService.SendAsync(deviceId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Cihazın son komutları — diyagramın özellikler panelinde görünür. </summary>
    [HttpGet("{deviceId:guid}/commands")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> GetCommands(Guid deviceId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var result = await _deviceCommandService.GetRecentAsync(deviceId, take, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
