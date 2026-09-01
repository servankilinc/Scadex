using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Queries;

public class CameraDto : IDto
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;
    public int RtspPort { get; set; }
    public int HttpPort { get; set; }
    public int? HttpsPort { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int MainStreamChannel { get; set; }
    public int SubStreamChannel { get; set; }
    public bool MainStreamEnabled { get; set; }
    public bool SubStreamEnabled { get; set; }
    public int SnapshotChannel { get; set; }

    public int? MonitoringPort { get; set; }
    public int? DeviceStatusId { get; set; }

    public string? DeviceStatusName { get; set; }
    public DateTime? LastSeen { get; set; }
    public int PingIntervalSec { get; set; }
    public bool IsMonitoringEnabled { get; set; }
    public string? LastConnectionError { get; set; }

    #region --- IAuditableEntity ---
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; } 
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; } 
    #endregion
}
