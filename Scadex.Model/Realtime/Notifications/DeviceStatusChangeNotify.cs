using Scadex.Core.Model;
using Scadex.Model.Enums;

namespace Scadex.Model.Realtime.Notifications;

public class DeviceStatusChangeNotify : IDto
{
    public Guid DeviceId { get; set; }
    public EntityEnums.DeviceStatus? StatusId { get; set; }
    public DateTime? LastSeen { get; set; }
}
