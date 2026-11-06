using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Business.Utils.DeviceObserver;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// Diyagram tablolarının tek yazım yolu. Editör her eylemi anında kendi ucuna gönderir (2026-10-27): cihaz / kablo / not
/// oluşturma ve özellik formu, tuval yerleşimi ve seçimi silme.
/// <para/>
/// Cihaz güncellenince / silinince <c>IDeviceChangeObserver</c>'lar commit SONRASI burada çağrılır (servisin içinde değil:
/// geri alınabilecek bir yazımdan önce dış dünyaya — ör. MediaMTX kamera yolu — dokunulmamalı).
/// </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class DiagramController : BaseController
{
    private readonly IDiagramService _diagramService;
    private readonly IEnumerable<IDeviceChangeObserver> _deviceObservers;
    private readonly ILogger<DiagramController> _logger;

    public DiagramController(ILogger<DiagramController> logger, IDiagramService diagramService, IEnumerable<IDeviceChangeObserver> deviceObservers) : base(logger)
    {
        _diagramService = diagramService;
        _deviceObservers = deviceObservers;
        _logger = logger;
    }

    [HttpGet("automation/{automationId:guid}")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> GetAutomationDiagram(Guid automationId, CancellationToken cancellationToken)
    {
        var result = await _diagramService.GetAsync(automationId, cancellationToken);
        return ToAction(result);
    }

    #region Cihaz
    [HttpPost("automation/{automationId:guid}/devices")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateDevice(Guid automationId, DeviceCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateDeviceAsync(automationId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("automation/{automationId:guid}/devices/{deviceId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateDevice(Guid automationId, Guid deviceId, DeviceUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateDeviceAsync(automationId, deviceId, request, cancellationToken);
        if (result.IsSuccess)
            await _deviceObservers.PublishDevicesChangedAsync([deviceId], _logger, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Kablo
    [HttpPost("automation/{automationId:guid}/connections")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateConnection(Guid automationId, ConnectionCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateConnectionAsync(automationId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("automation/{automationId:guid}/connections/{connectionId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateConnection(Guid automationId, Guid connectionId, ConnectionUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateConnectionAsync(automationId, connectionId, request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Not
    [HttpPost("automation/{automationId:guid}/annotations")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateAnnotation(Guid automationId, DiagramAnnotationCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateAnnotationAsync(automationId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("automation/{automationId:guid}/annotations/{annotationId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateAnnotation(Guid automationId, Guid annotationId, DiagramAnnotationUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateAnnotationAsync(automationId, annotationId, request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Yerleşim ve silme
    [HttpPut("automation/{automationId:guid}/layout")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateLayout(Guid automationId, DiagramLayoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateLayoutAsync(automationId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("automation/{automationId:guid}/delete")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> Delete(Guid automationId, DiagramDeleteRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.DeleteAsync(automationId, request, cancellationToken);
        if (result.IsSuccess)
            await _deviceObservers.PublishDevicesChangedAsync(request.DeviceIds, _logger, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
