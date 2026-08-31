using Scadex.Core.Model;
using Scadex.Model.Entities.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

/// <summary>
/// Anlık görüntü — ISAPI: <c>http://{IpAddress}:{HttpPort}/ISAPI/Streaming/channels/{SnapshotChannel}/picture</c>. <para/>
/// Canlı yayın — RTSP: <c>rtsp://{Username}:{Password}@{IpAddress}:{RtspPort}/Streaming/Channels/{kanal}</c>
/// </summary>
public class Camera : IEntity, IAuditableEntity, IActivatableEntity, IMonitoredAsset
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    #region Network
    public string IpAddress { get; set; } = null!;
    /// <summary>Varsayılanı 554.</summary>
    public int RtspPort { get; set; }
    /// <summary>Varsayılanı 80.</summary>
    public int HttpPort { get; set; }
    /// <summary>kamerada TLS kapalıysa <c>null</c>.</summary>
    public int? HttpsPort { get; set; } 
    #endregion

    #region Erişim
    public string? Username { get; set; }
    public string? Password { get; set; } 
    #endregion

    #region Akış
    /// <summary> Main stream kanal numarası Hikvision'da 101. </summary>
    public int MainStreamChannel { get; set; }

    /// <summary> Sub stream kanal numarası Hikvision'da 102. </summary>
    public int SubStreamChannel { get; set; }

    /// <summary> Main stream kullanılabilir mi? </summary>
    public bool MainStreamEnabled { get; set; }

    /// <summary> Sub stream kullanılabilir mi? Kapalıysa liste ekranın da yayın gösterilmez sadece anlık görüntü alabilir veya detaylı şekilde main stream alabilir. </summary>
    public bool SubStreamEnabled { get; set; }
    #endregion

    #region Anlık görüntü
    /// <summary> ISAPI snapshot kanal numarası. Main stream(101) veya Sub stream(102) ile alınabilir. </summary>
    public int SnapshotChannel { get; set; }
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
