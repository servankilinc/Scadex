using Scadex.Core.Model;

namespace Scadex.Model.Realtime.Notifications;

public class ChannelValueChangeNotify : IDto
{
    public Guid IoChannelId { get; set; }
    public Guid DeviceId { get; set; }
    public int ChannelNumber { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
