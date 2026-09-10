using Scadex.Business.Settings;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.CameraCaptureSetting.Commands;
using Scadex.Model.Dtos.CameraCaptureSetting.Queries;

namespace Scadex.Business.Abstract;

public interface ICameraCaptureSettingService
{
    Task<CameraCaptureSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<CameraCaptureSettingDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(CameraCaptureSettingUpdateDto request, CancellationToken cancellationToken = default);
}
