using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.CameraProtocolProfile;
using Scadex.Model.Entities;

namespace Scadex.Business.Utils.CameraProtocolProfile.Resolver;

public interface ICameraProtocolProfileResolver
{
    ICameraProtocolProfile Resolve(Camera camera);
}

public class CameraProtocolProfileResolver : ICameraProtocolProfileResolver
{
    private readonly IReadOnlyList<ICameraProtocolProfile> _profiles;
    private readonly ICameraProtocolProfile _fallback;
    private readonly ILogger<CameraProtocolProfileResolver> _logger;

    public CameraProtocolProfileResolver(IEnumerable<ICameraProtocolProfile> profiles, ILogger<CameraProtocolProfileResolver> logger)
    {
        _profiles = profiles.ToList();
        _logger = logger;

        // varsayılan profil: Hikvision varsa onu, yoksa listedeki ilk profil. Hiçbir profil yoksa hata fırlatılır.
        _fallback = 
            _profiles.FirstOrDefault(p => p.Manufacturer.Equals("Hikvision", StringComparison.OrdinalIgnoreCase)) ?? 
            _profiles.FirstOrDefault() ?? 
            throw new InvalidOperationException("Hiçbir ICameraProtocolProfile Kayıtlı değil.");
    }

    public ICameraProtocolProfile Resolve(Camera camera)
    {
        if (!string.IsNullOrWhiteSpace(camera.Manufacturer))
        {
            var match = _profiles.FirstOrDefault(p => p.Manufacturer.Equals(camera.Manufacturer, StringComparison.OrdinalIgnoreCase));

            if (match != null) return match;

            _logger.LogWarning(
                "Kamera {CameraId} icin '{Manufacturer}' ureticisine ait bir protokol profili yok; {Fallback} varsayılıyor. " +
                "URL sablonlari uymuyorsa kamera erisilemez gorunecektir.",
                camera.Id, camera.Manufacturer, _fallback.Manufacturer);
        }

        return _fallback;
    }
}