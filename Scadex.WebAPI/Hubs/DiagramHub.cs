using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Scadex.WebAPI.Hubs;

[Authorize]
[DisableRateLimiting]
public class DiagramHub : Hub<IDiagramHubClientContract>
{
    public static string GroupName(Guid automationId) => $"automation:{automationId}";

    /// <summary> Tum otomasyonların durum ozeti (ana sayfa haritasi, otomasyon listesi ekranlarında). Yalnizca <c>AutomationStatusChanged</c> gider, o da yalnizca durum degisince. </summary>
    public const string OverviewGroupName = "automations";

    /// <summary> Client gönderdiği otomasyonun gurubuna abone olup o otomasyonun değişikliklerini dinlemek istediğini bilrdiriyor </summary>
    public Task Subscribe(Guid automationId)
    {
        return this.Groups.AddToGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: GroupName(automationId),
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client gönderdiği otomasyonun değişikliklerini dinlemek istemediğini bildiriyor </summary>
    public Task Unsubscribe(Guid automationId)
    {
        return this.Groups.RemoveFromGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: GroupName(automationId),
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client tum otomasyonların durum degisikliklerini dinlemek istedigini bildiriyor </summary>
    public Task SubscribeAutomations()
    {
        return this.Groups.AddToGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: OverviewGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client tum otomasyonların durum degisikliklerini dinlemek istemedigini bildiriyor </summary>
    public Task UnsubscribeAutomations()
    {
        return this.Groups.RemoveFromGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: OverviewGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }
}
