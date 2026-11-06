using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Commands;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Queries;
using Scadex.CameraManagement.Model.Settings;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.CameraManagement.Services.Abstract;

public interface ICameraCaptureSettingService
{
    Task<CameraCaptureSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<CameraCaptureSettingDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(CameraCaptureSettingUpdateDto request, CancellationToken cancellationToken = default);
}
