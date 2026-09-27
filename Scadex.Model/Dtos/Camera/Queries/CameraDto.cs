using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Queries;

public class CameraDto : IDto
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;

    public string? Username { get; set; }

    public string? Password { get; set; }

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
