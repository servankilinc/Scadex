using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ChannelEvent.Queries;

public class ChannelEventDto : IDto
{
    public long Id { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid CabinetId { get; set; }
    public string? ChannelName { get; set; }
    public int? ChannelNumber { get; set; }

    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceExternalCode { get; set; }

    public string Value { get; set; } = null!;
    public string? PreviousValue { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
