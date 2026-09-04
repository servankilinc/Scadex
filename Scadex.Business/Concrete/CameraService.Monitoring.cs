using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Commands;
using DeviceStatusEnum = Scadex.Model.Enums.EntityEnums.DeviceStatus;

namespace Scadex.Business.Concrete;

public partial class CameraService
{
    /// <inheritdoc/>
    public async Task<Result> RecordProbeResultAsync(Guid cameraId, CameraProbeResultDto result, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(result, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for CameraProbeResultDto");

        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == cameraId, tracking: true, cancellationToken: cancellationToken);

        if (camera == null)
            return Result.NotFound(description: "Kamera bulunamadi");

        var nextStatus = result.Reachable ? (int)DeviceStatusEnum.Online : (int)DeviceStatusEnum.Offline;
        var nextError = result.Reachable ? null : result.Error?.Truncate(512);

        bool statusChanged = camera.DeviceStatusId != nextStatus;
        bool errorChanged = camera.LastConnectionError != nextError;

        if (result.Reachable)
            camera.LastSeen = DateTime.UtcNow;

        if (!statusChanged && !errorChanged && !result.Reachable)
            return Result.Success();

        camera.DeviceStatusId = nextStatus;
        camera.LastConnectionError = nextError;

        await _unitOfWork.Cameras.UpdateAndSaveAsync(camera, cancellationToken);
        return Result.Success();
    }
}
