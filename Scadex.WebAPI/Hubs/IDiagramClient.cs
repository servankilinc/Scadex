using Scadex.Model.Realtime.Notifications;

namespace Scadex.WebAPI.Hubs;

/// <summary> Hub'in Client arayüzü, Client'lara hangi mesajların gönderilebileceğini tanımlıyorum. </summary>
public interface IDiagramHubClientContract
{
    Task ChannelValuesChanged(Guid automationId, IReadOnlyList<ChannelValueChangeNotify> changes);

    Task DeviceStatusChanged(Guid automationId, IReadOnlyList<DeviceStatusChangeNotify> changes);

    Task AutomationStatusChanged(AutomationStatusChangeNotify change);

    Task CommandCompleted(Guid automationId, CommandCompletedNotify change);

    Task CardRead(Guid automationId, CardReadNotify change);
}
