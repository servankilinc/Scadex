using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

/// <summary>
/// İzleme ayarları RTSP adresi, port, kanal vs. desteği markaya aittir: <see cref="Brand"/>'e karşılık gelen <c>ICameraProtocolProfile</c> üretir.
/// (Hikvision) RTSP: <c>rtsp://{Username}:{Password}@{IpAddress}:554/Streaming/Channels/{101|102}</c> — bu adrese YALNIZCA MediaMTX bağlanır.
/// Canlı yayın, anlık görüntü ve klip (FFmpeg, ana akım) MediaMTX'in yolundan okunur; marka API'si (ISAPI) kullanılmaz.
/// </summary>
public class Camera : IEntity
{
    public Guid DeviceId { get; set; }
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }

    #region Erişim
    public string? Username { get; set; }
    public string? Password { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    public virtual ICollection<CameraCapture>? Captures { get; set; }
    #endregion
}
