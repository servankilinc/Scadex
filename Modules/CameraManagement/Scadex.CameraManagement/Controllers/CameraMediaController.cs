using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.Auth;
using Scadex.CameraManagement.Controllers.Base;
using Scadex.CameraManagement.Model.Dtos.Capture.Commands;
using Scadex.CameraManagement.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.CameraManagement.Controllers;

/// <summary>
/// Kameranın medya uçları. Çekirdekteki <c>CameraController</c> (cihaz bilgisi: liste/detay/güncelleme) ile AYNI <c>api/Camera</c> önekini
/// paylaşır — ön yüz adresleri modülleşmeden önceki gibidir. Modül kapalıyken bu uçlar 404'tür, cihaz bilgisi uçları kalır.
/// </summary>
[Route("api/Camera")]
public class CameraMediaController : CameraManagementControllerBase
{
    private readonly ICameraMediaService _cameraMediaService;

    public CameraMediaController(ILogger<CameraMediaController> logger, ICameraMediaService cameraMediaService) : base(logger)
    {
        _cameraMediaService = cameraMediaService;
    }

    #region Streaming (MediaGateway)
    [HttpPost("{id:guid}/stream-ticket")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> CreateStreamTicket(Guid id, [FromQuery] StreamProfile profile, CancellationToken cancellationToken)
    {
        var result = await _cameraMediaService.CreateStreamTokenAsync(id, profile, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Snapshot
    [HttpGet("{id:guid}/snapshot")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> GetSnapshot(Guid id, [FromQuery] bool fresh, CancellationToken cancellationToken)
    {
        var result = await _cameraMediaService.GetSnapshotAsync(id, fresh, cancellationToken);
        if (!result.IsSuccess) return ToAction(result);

        return File(result.Data.Content, result.Data.ContentType);
    }
    #endregion

    #region Captured
    [HttpPost("{id:guid}/capture")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> CreateCapture(Guid id, CameraCaptureCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraMediaService.CreateCaptureAsync(id, request, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/captures")]
    [RequirePermission(Permission.ViewCamera)]
    public async Task<IActionResult> GetCaptures(Guid id, [FromQuery] int take, CancellationToken cancellationToken)
    {
        var result = await _cameraMediaService.GetCapturesAsync(id, take <= 0 ? 20 : take, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
