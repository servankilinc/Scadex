using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Pin.Queries;

public class PinDetailDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public Guid? IoChannelId { get; set; }
    public string? IoChannelName { get; set; }
    public PinFunction Function { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = null!;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDateUtc { get; set; }
}