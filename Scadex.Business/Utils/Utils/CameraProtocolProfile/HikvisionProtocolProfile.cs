using CabinetOs.Model.Entities;
using Microsoft.Extensions.Logging;
using static CabinetOs.Model.Enums.EntityEnums;

namespace CabinetOs.Business.Utils.CameraProtocolProfile;

/// <summary>
/// Hikvision URL sablonlari. Bugun hedeflenen donanim <c>DS-2CD1123G0-IUF</c>.
///
/// Kanal numaralari SABIT DEGILDIR (<c>MainStreamChannel</c> /
/// <c>SubStreamChannel</c> / <c>SnapshotChannel</c>): NVR arkasindaki bir
/// kamerada numaralar farklidir.
/// </summary>
public class HikvisionProtocolProfile : ICameraProtocolProfile
{
    public string Manufacturer => "Hikvision";

    public string BuildRtspUrl(Camera camera, StreamProfile profile)
    {
        int channel = profile == StreamProfile.Main ? camera.MainStreamChannel : camera.SubStreamChannel;

        // Kimlik bilgisi URL'in icinde: MediaMTX'in RTSP kaynagi bu bicimi bekler.
        // Bu metin YALNIZCA loopback uzerinden MediaMTX'e gider; tarayiciya
        // donen hicbir govdede yer almaz.
        return $"rtsp://{camera.Username}:{camera.Password}@{camera.IpAddress}:{camera.RtspPort}/Streaming/Channels/{channel}";
    }

    // Hikvision'da RTSP yolunda "Channels" buyuk, ISAPI yolunda "channels" kucuk
    // harfle yazilir. Yazim hatasi degil — firmware ikisini de bu sekilde bekler.
    public string BuildSnapshotPath(Camera camera) =>
        $"/ISAPI/Streaming/channels/{camera.SnapshotChannel}/picture";
}

/// <summary>
/// <see cref="ICameraProtocolProfile"/> secici.
///
/// Eslesme bulunamazsa Hikvision'a duser ve UYARI LOGLAR: sessizce yanlis marka
/// varsaymak, "kamera cevap vermiyor" gibi gorunen ama aslinda yanlis URL'den
/// kaynaklanan, teshisi zor bir hata uretirdi.
/// </summary>
public class CameraProtocolProfileResolver : ICameraProtocolProfileResolver
{
    private readonly IReadOnlyList<ICameraProtocolProfile> _profiles;
    private readonly ICameraProtocolProfile _fallback;
    private readonly ILogger<CameraProtocolProfileResolver> _logger;

    public CameraProtocolProfileResolver(
        IEnumerable<ICameraProtocolProfile> profiles,
        ILogger<CameraProtocolProfileResolver> logger)
    {
        _profiles = profiles.ToList();
        _logger = logger;

        _fallback = _profiles.FirstOrDefault(p => p.Manufacturer.Equals("Hikvision", StringComparison.OrdinalIgnoreCase))
            ?? _profiles.FirstOrDefault()
            ?? throw new InvalidOperationException("Hicbir ICameraProtocolProfile kayitli degil.");
    }

    public ICameraProtocolProfile Resolve(Camera camera)
    {
        if (!string.IsNullOrWhiteSpace(camera.Manufacturer))
        {
            var match = _profiles.FirstOrDefault(p =>
                p.Manufacturer.Equals(camera.Manufacturer, StringComparison.OrdinalIgnoreCase));

            if (match != null) return match;

            _logger.LogWarning(
                "Kamera {CameraId} icin '{Manufacturer}' ureticisine ait bir protokol profili yok; {Fallback} varsayiliyor. " +
                "URL sablonlari uymuyorsa kamera erisilemez gorunecektir.",
                camera.Id, camera.Manufacturer, _fallback.Manufacturer);
        }

        return _fallback;
    }
}
