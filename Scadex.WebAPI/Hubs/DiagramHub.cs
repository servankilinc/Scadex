using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Scadex.WebAPI.Hubs;

[Authorize]
[DisableRateLimiting]
public class DiagramHub : Hub<IDiagramHubClientContract>
{
    public static string GroupName(Guid cabinetId) => $"cabinet:{cabinetId}";

    /// <summary> Tum kabinlerin durum ozeti (ana sayfa haritasi, kabin listesi ekranlarında). Yalnizca <c>CabinetStatusChanged</c> gider, o da yalnizca durum degisince. </summary>
    public const string OverviewGroupName = "cabinets";

    /// <summary> Client gönderdiği kabinin gurubuna abone olup o kabinin değişikliklerini dinlemek istediğini bilrdiriyor </summary>
    public Task Subscribe(Guid cabinetId)
    {
        return this.Groups.AddToGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: GroupName(cabinetId),
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client gönderdiği kabinin değişikliklerini dinlemek istemediğini bildiriyor </summary>
    public Task Unsubscribe(Guid cabinetId)
    {
        return this.Groups.RemoveFromGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: GroupName(cabinetId),
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client tum kabinlerin durum degisikliklerini dinlemek istedigini bildiriyor </summary>
    public Task SubscribeCabinets()
    {
        return this.Groups.AddToGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: OverviewGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client tum kabinlerin durum degisikliklerini dinlemek istemedigini bildiriyor </summary>
    public Task UnsubscribeCabinets()
    {
        return this.Groups.RemoveFromGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: OverviewGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }
}
