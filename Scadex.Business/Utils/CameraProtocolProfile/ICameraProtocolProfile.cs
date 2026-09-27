using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile;

/// <summary>
/// Bir kamera markasının protokol ayrıntıları: RTSP yolu, portu, kanal numaraları, sub stream desteğini belirleyen <c>Camera.Brand</c>.
/// Kamerayalar ile MediaMTX aracılığı (RTSP) ile canlı izleme, anlık görüntü ve klip MediaMTX'in path'leri ile yönetilir. Marka API'si (ISAPI vb.) kullanılmaz.
/// </summary>
public interface ICameraProtocolProfile
{
    /// <summary> Bu profilin karşıladığı marka. Her <see cref="CameraBrand"/> değerinin sadece bir profili olmalı. </summary>
    CameraBrand Brand { get; }
    int RtspPort { get; }

    /// <summary>
    /// Markada ayrı bir düşük çözünürlüklü akım(sub stream) var mı? <c>false</c> ise <see cref="StreamProfile.Sub"/> isteği
    /// <see cref="StreamProfile.Main"/> yoluna düşer — kameraya aynı akım için ikinci bir RTSP bağlantısı açılmaz.
    /// </summary>
    bool HasSubStream { get; }

    /// <summary> Kameranın RTSP adresi; YALNIZCA Media Gateway yolunun kaynağı olur (<c>EnsureLivePathAsync</c>). Client'a ASLA gitmez icinde kamera parolasi var; loga da maskelenmeden yazılmaz. </summary>
    string BuildRtspUrl(Camera camera, StreamProfile profile);
}
