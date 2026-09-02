using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.IoChannel.Queries;

public class IoChannelDto : IDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public int ChannelNumber { get; set; }
    public PinDirection Direction { get; set; }
    public bool IsEnabled { get; set; }
    public string? CurrentValue { get; set; }
    public string Name { get; set; } = null!;
    public DateTime? ValueUpdatedAt { get; set; }
    public bool IsEventLogged { get; set; }
    public string? EventTriggerValue { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDateUtc { get; set; }
}