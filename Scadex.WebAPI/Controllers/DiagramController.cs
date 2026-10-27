using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// Diyagram tablolarının tek yazım yolu. Editör her eylemi anında kendi ucuna gönderir (2026-10-27): cihaz / kablo / not
/// oluşturma ve özellik formu, tuval yerleşimi ve seçimi silme.
/// </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class DiagramController : BaseController
{
    private readonly IDiagramService _diagramService;
    private readonly ICameraService _cameraService;

    public DiagramController(ILogger<DiagramController> logger, IDiagramService diagramService, ICameraService cameraService) : base(logger)
    {
        _diagramService = diagramService;
        _cameraService = cameraService;
    }

    [HttpGet("cabinet/{cabinetId:guid}")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> GetCabinetDiagram(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _diagramService.GetAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    #region Cihaz
    [HttpPost("cabinet/{cabinetId:guid}/devices")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateDevice(Guid cabinetId, DeviceCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateDeviceAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("cabinet/{cabinetId:guid}/devices/{deviceId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateDevice(Guid cabinetId, Guid deviceId, DeviceUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateDeviceAsync(cabinetId, deviceId, request, cancellationToken);
        if (result.IsSuccess)
            await RefreshStreamPathsAsync([deviceId], cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Kablo
    [HttpPost("cabinet/{cabinetId:guid}/connections")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateConnection(Guid cabinetId, ConnectionCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateConnectionAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("cabinet/{cabinetId:guid}/connections/{connectionId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateConnection(Guid cabinetId, Guid connectionId, ConnectionUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateConnectionAsync(cabinetId, connectionId, request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Not
    [HttpPost("cabinet/{cabinetId:guid}/annotations")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> CreateAnnotation(Guid cabinetId, DiagramAnnotationCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.CreateAnnotationAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut("cabinet/{cabinetId:guid}/annotations/{annotationId:guid}")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateAnnotation(Guid cabinetId, Guid annotationId, DiagramAnnotationUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateAnnotationAsync(cabinetId, annotationId, request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Yerleşim ve silme
    [HttpPut("cabinet/{cabinetId:guid}/layout")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> UpdateLayout(Guid cabinetId, DiagramLayoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.UpdateLayoutAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("cabinet/{cabinetId:guid}/delete")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> Delete(Guid cabinetId, DiagramDeleteRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.DeleteAsync(cabinetId, request, cancellationToken);
        if (result.IsSuccess)
            await RefreshStreamPathsAsync(request.DeviceIds, cancellationToken);
        return ToAction(result);
    }
    #endregion

    /// <summary>
    /// Commit SONRASI: kamera bilgisi değişen ya da silinen kameraların MediaMTX yolları düşürülür, açık izleyicinin eski
    /// bilgilerle kurulan oturumu kesilir. Servisin içinde değil burada, çünkü yol, geri alınabilecek bir yazımdan önce
    /// silinmemeli. Kamera olmayan Id'ler yok sayılır; başarısızlık yalnızca loglanır.
    /// </summary>
    private Task RefreshStreamPathsAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken) =>
        deviceIds.Count == 0 ? Task.CompletedTask : _cameraService.RefreshStreamPathsAsync(deviceIds, cancellationToken);
}
