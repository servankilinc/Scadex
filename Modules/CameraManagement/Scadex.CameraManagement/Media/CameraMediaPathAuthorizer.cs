using Scadex.Business.Utils.MediaGateway;
using Scadex.CameraManagement.Services.Abstract;
using Scadex.Model.Dtos.MediaGateway;

namespace Scadex.CameraManagement.Media;

/// <summary>
/// MediaMTX auth kancasında <c>cam_</c> yollarını yetkilendirir. <b>Kamerada YALNIZCA OKUMA vardır</b>: kameraya publish yoktur, kaynağı
/// MediaMTX kendisi çeker. Okuma, tarayıcının (WHEP) ya da sunucu içi FFmpeg'in yola bağlı kısa ömürlü biletiyle doğrulanır.
/// </summary>
public sealed class CameraMediaPathAuthorizer : IMediaPathAuthorizer
{
    private readonly ICameraMediaService _cameraMediaService;

    public CameraMediaPathAuthorizer(ICameraMediaService cameraMediaService) => _cameraMediaService = cameraMediaService;

    public bool CanHandle(string path) => CameraMediaPaths.IsManagedPathName(path);

    public async Task<bool> AuthorizeAsync(MediaMtxAuthDto request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Action, "read", StringComparison.OrdinalIgnoreCase))
            return false;

        return await _cameraMediaService.ValidateStreamTokenAsync(request.Path, request.Password, cancellationToken);
    }
}
