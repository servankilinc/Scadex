using Microsoft.AspNetCore.SignalR;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Model.Dtos.Realtime.Queries;
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

    public async Task ChannelValuesChangedAsync(Guid cabinetId, IReadOnlyList<ChannelValueChange> changes, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(cabinetId);
            await _hub.Clients.Group(groupName).ChannelValuesChanged(cabinetId, changes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: ChannelValuesChangedAsync, kabin {cabinetId}");
        }
    }

    public async Task DeviceStatusesChangedAsync(Guid cabinetId, IReadOnlyList<DeviceStatusChange> changes, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(cabinetId);
            await _hub.Clients.Group(groupName).DeviceStatusChanged(cabinetId, changes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: DeviceStatusesChangedAsync, kabin {cabinetId}");
        }
    }

    public async Task CabinetStatusChangedAsync(CabinetStatusChange change, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(change.CabinetId);
            await _hub.Clients.Group(groupName).CabinetStatusChanged(change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: CabinetStatusChangedAsync, kabin {change.CabinetId}");
        }
    }

    public async Task CommandCompletedAsync(Guid cabinetId, CommandCompleted change, CancellationToken cancellationToken = default)
    {
        try
        {
            string groupName = DiagramHub.GroupName(cabinetId);
            await _hub.Clients.Group(groupName).CommandCompleted(cabinetId, change);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Canli yayin basarisiz: CommandCompletedAsync, kabin {cabinetId}");
        }
    }
}
