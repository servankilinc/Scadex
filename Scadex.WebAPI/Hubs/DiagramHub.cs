using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Scadex.WebAPI.Hubs;

[Authorize]
[DisableRateLimiting]
public class DiagramHub : Hub<IDiagramHubClientContract>
{
    public static string GroupName(Guid cabinetId) => $"cabinet:{cabinetId}";

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
}
