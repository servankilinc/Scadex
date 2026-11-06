using Microsoft.AspNetCore.SignalR;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Model.Realtime.Notifications;
using Scadex.WebAPI.Hubs;

namespace Scadex.WebAPI.Utils;

public class DiagramNotifier : IDiagramNotifier
{
    private readonly IHubContext<DiagramHub, IDiagramHubClientContract> _hub;
    private readonly ILogger<DiagramNotifier> _logger;

    public DiagramNotifier(IHubContext<DiagramHub, IDiagramHubClientContract> hub, ILogger<DiagramNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task ChannelValuesChangedAsync(Guid automationId, IReadOnlyList<ChannelValueChangeNotify> changes, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(automationId);
            await _hub.Clients.Group(groupName).ChannelValuesChanged(automationId, changes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: ChannelValuesChangedAsync, otomasyon {automationId}");
        }
    }

    public async Task DeviceStatusesChangedAsync(Guid automationId, IReadOnlyList<DeviceStatusChangeNotify> changes, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(automationId);
            await _hub.Clients.Group(groupName).DeviceStatusChanged(automationId, changes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: DeviceStatusesChangedAsync, otomasyon {automationId}");
        }
    }

    public async Task AutomationStatusChangedAsync(AutomationStatusChangeNotify change, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(change.AutomationId);
            await _hub.Clients.Group(groupName).AutomationStatusChanged(change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: AutomationStatusChangedAsync, otomasyon {change.AutomationId}");
        }
    }

    public async Task AutomationStatusChangedForAllAsync(AutomationStatusChangeNotify change, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(DiagramHub.OverviewGroupName).AutomationStatusChanged(change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: AutomationStatusChangedForAllAsync, otomasyon {change.AutomationId}");
        }
    }

    public async Task CommandCompletedAsync(Guid automationId, CommandCompletedNotify change, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(automationId);
            await _hub.Clients.Group(groupName).CommandCompleted(automationId, change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: CommandCompletedAsync, otomasyon {automationId}");
        }
    }

    public async Task CardReadAsync(Guid automationId, CardReadNotify change, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(automationId);
            await _hub.Clients.Group(groupName).CardRead(automationId, change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: CardReadAsync, otomasyon {automationId}");
        }
    }
}
