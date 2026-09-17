using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Scadex.Signalization.Hubs;

/// <summary>
/// Sinyalizasyon modulunun canli yayini — <c>/hubs/signalization</c>, yalnizca modul acikken eslenir
/// (<see cref="ServiceRegistration.MapSignalizationModule"/>). Cekirdegin <c>DiagramHub</c>'i ile ayni kalip.
/// </summary>
[Authorize]
[DisableRateLimiting]
public class SignalizationHub : Hub<ISignalizationHubClientContract>
{
    /// <summary> Tum kabinlerin operator islemleri. Kabin bazli grup BILINCLI yok: canli panel, harita ve uyari izleyicisi zaten tum kabinleri dinler. </summary>
    public const string SessionsGroupName = "sessions";

    /// <summary> Client operator islemi degisikliklerini dinlemek istedigini bildiriyor </summary>
    public Task SubscribeSessions()
    {
        return this.Groups.AddToGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: SessionsGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }

    /// <summary> Client operator islemi degisikliklerini dinlemek istemedigini bildiriyor </summary>
    public Task UnsubscribeSessions()
    {
        return this.Groups.RemoveFromGroupAsync(
            connectionId: Context.ConnectionId,
            groupName: SessionsGroupName,
            cancellationToken: Context.ConnectionAborted
        );
    }
}
