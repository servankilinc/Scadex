using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CameraController : BaseController
{
    private readonly ICameraService _cameraService;

    public CameraController(ILogger<CameraController> logger, ICameraService cameraService) : base(logger)
    {
        _cameraService = cameraService;
    }

    #region CRUD
    /// <summary>Bir kabindeki kameralar.</summary>
    [HttpGet("cabinet/{cabinetId:guid}")]
    public async Task<IActionResult> ListByCabinet(Guid cabinetId, [FromQuery] bool includePassive, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetListAsync(cabinetId, includePassive, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetAsync(id, cancellationToken);
        return ToAction(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CameraCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraService.CreateAsync(request, cancellationToken);
        return ToAction(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(CameraUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraService.UpdateAsync(request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Monitoring
    [HttpPost("{id:guid}/probe-result")]
    public async Task<IActionResult> RecordProbeResult(Guid id, CameraProbeResultDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraService.RecordProbeResultAsync(id, request, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Streaming (MediaGateway)
    [HttpPost("{id:guid}/stream-ticket")]
    public async Task<IActionResult> CreateStreamTicket(Guid id, [FromQuery] StreamProfile profile, CancellationToken cancellationToken)
    {
        var result = await _cameraService.CreateStreamTokenAsync(id, profile, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Snapshot
    [HttpGet("{id:guid}/snapshot")]
    public async Task<IActionResult> GetSnapshot(Guid id, [FromQuery] bool fresh, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetSnapshotAsync(id, fresh, cancellationToken);
        if (!result.IsSuccess) return ToAction(result);

        return File(result.Data.Content, result.Data.ContentType);
    }
    #endregion

    #region Captured
    [HttpPost("{id:guid}/capture")]
    public async Task<IActionResult> CreateCapture(Guid id, CameraCaptureCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _cameraService.CreateCaptureAsync(id, request, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/captures")]
    public async Task<IActionResult> GetCaptures(Guid id, [FromQuery] int take, CancellationToken cancellationToken)
    {
        var result = await _cameraService.GetCapturesAsync(id, take <= 0 ? 20 : take, cancellationToken);
        return ToAction(result);
    }
    #endregion
}
