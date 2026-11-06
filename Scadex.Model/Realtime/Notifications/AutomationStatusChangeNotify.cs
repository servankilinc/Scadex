using Scadex.Core.Model;
using Scadex.Model.Enums;

namespace Scadex.Model.Realtime.Notifications;

public class AutomationStatusChangeNotify : IDto
{
    public Guid AutomationId { get; set; }
    public EntityEnums.DeviceStatus? StatusId { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? ScadaLastIngestAt { get; set; }
}
