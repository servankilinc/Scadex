using Scadex.Business.Utils.DeviceObserver;
using Scadex.CameraManagement.Services.Abstract;

namespace Scadex.CameraManagement.Services.Concrete;

/// <summary>
/// Diyagramdan ya da kamera ekranından cihaz bilgisi değişince / cihaz kaldırılınca (commit sonrası) kameraların MediaMTX yollarını yeniler.
/// Kamera olmayan Id'leri <see cref="ICameraMediaService.RefreshStreamPathsAsync"/> zaten ayıklar.
/// </summary>
public sealed class CameraDeviceChangeObserver : IDeviceChangeObserver
{
    private readonly ICameraMediaService _cameraMediaService;

    public CameraDeviceChangeObserver(ICameraMediaService cameraMediaService) => _cameraMediaService = cameraMediaService;

    public Task OnDevicesChangedAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken = default) =>
        _cameraMediaService.RefreshStreamPathsAsync(deviceIds, cancellationToken);
}
