using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scadex.RemoteDesk.DataAccess;
using Scadex.RemoteDesk.Model.Entities;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Control;

/// <summary>
/// <c>RemoteControlSession</c> yazımı — tablonun TEK yazım yolu. Koordinatör singleton olduğu için her işlem kendi scope'unda yapılır
/// (<see cref="Streaming.ScreenSessionStore"/> ile aynı kalıp).
/// </summary>
public sealed class RemoteControlStore
{
    private readonly IServiceScopeFactory _scopes;

    public RemoteControlStore(IServiceScopeFactory scopes) => _scopes = scopes;

    public Task StartAsync(Guid id, Guid deviceId, Guid userId, CancellationToken cancellationToken = default) =>
        WithDbAsync(async db =>
        {
            db.RemoteControlSessions.Add(new RemoteControlSession
            {
                Id = id,
                DeviceId = deviceId,
                UserId = userId,
                StartedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        });

    /// <summary> Kapalı oturuma dokunmaz. </summary>
    public Task EndAsync(Guid id, RemoteControlEndReason reason, int inputEventCount, CancellationToken cancellationToken = default) =>
        WithDbAsync(db => db.RemoteControlSessions
            .Where(c => c.Id == id && c.EndedUtc == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.EndedUtc, DateTime.UtcNow)
                .SetProperty(c => c.EndReason, reason)
                .SetProperty(c => c.InputEventCount, inputEventCount), cancellationToken));

    /// <summary> Açılışta: önceki süreçten açık kalan kontrol oturumları kapatılır (canlı durum bellekteydi, sürdürülemez). </summary>
    public async Task<int> CloseOrphansAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        await WithDbAsync(async db =>
        {
            count = await db.RemoteControlSessions
                .Where(c => c.EndedUtc == null)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(c => c.EndedUtc, DateTime.UtcNow)
                    .SetProperty(c => c.EndReason, RemoteControlEndReason.ServerRestart), cancellationToken);
        });
        return count;
    }

    private async Task WithDbAsync(Func<RemoteDeskDbContext, Task> work)
    {
        using var scope = _scopes.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<RemoteDeskDbContext>());
    }
}
