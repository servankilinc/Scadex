using Scadex.Core.Model;
using Scadex.Model.Entities.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

/// <summary>
/// İzleme ayarları RTSP adresi, port, kanal vs. desteği markaya aittir: <see cref="Brand"/>'e karşılık gelen <c>ICameraProtocolProfile</c> üretir.
/// (Hikvision) RTSP: <c>rtsp://{Username}:{Password}@{IpAddress}:554/Streaming/Channels/{101|102}</c> — bu adrese YALNIZCA MediaMTX bağlanır.
/// Canlı yayın, anlık görüntü ve klip (FFmpeg, ana akım) MediaMTX'in yolundan okunur; marka API'si (ISAPI) kullanılmaz.
/// </summary>
public class Camera : IEntity, IAuditableEntity, IActivatableEntity, IMonitoredAsset
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }

    #region Network
    /// <summary>Monitoring ile ilgili alanlar (IpAddress hem monitoring için kullanılır hem de görüntü için)</summary>
    public string IpAddress { get; set; } = null!;
    #endregion

    #region Erişim
    public string? Username { get; set; }
    public string? Password { get; set; }
    #endregion

    #region İzleme IMonitoredAsset
    /// <inheritdoc/>
    public int? MonitoringPort { get; set; }
    public int? DeviceStatusId { get; set; }
    public DateTime? LastSeen { get; set; }
    public int PingIntervalSec { get; set; }
    public bool IsMonitoringEnabled { get; set; }
    public string? LastConnectionError { get; set; }
    #endregion



    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Cabinet? Cabinet { get; set; }
    public virtual DeviceStatus? DeviceStatus { get; set; }
    public virtual ICollection<CameraCapture>? Captures { get; set; } 
    #endregion
}
