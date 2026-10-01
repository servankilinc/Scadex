using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scadex.RemoteDesk.DataAccess;
using Scadex.RemoteDesk.Model.Entities;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Streaming;

/// <summary>
/// <c>ScreenSession</c> / <c>ScreenViewLog</c> yazımı — bu tabloların TEK yazım yolu. Koordinatör singleton olduğu için her işlem kendi
/// scope'unda (kendi DbContext'iyle) yapılır. Satırlar denetim içindir; canlı durum bellektedir (<see cref="ScreenStreamCoordinator"/>).
/// </summary>
public sealed class ScreenSessionStore
{
    private readonly IServiceScopeFactory _scopes;

    public ScreenSessionStore(IServiceScopeFactory scopes) => _scopes = scopes;

    public Task CreateSessionAsync(Guid sessionId, Guid deviceId, int monitorIndex, string mediaPath, CancellationToken cancellationToken = default) =>
        WithDbAsync(async db =>
        {
            db.ScreenSessions.Add(new ScreenSession
            {
                Id = sessionId,
                DeviceId = deviceId,
                MonitorIndex = monitorIndex,
                MediaPath = mediaPath,
                // Satır komut gönderilirken yazılır; "Created" yalnızca bellekte kısa bir andır.
                Status = ScreenSessionStatus.CommandSent,
                CreatedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        });

    public Task MarkStreamingAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        WithDbAsync(db => db.ScreenSessions
            .Where(s => s.Id == sessionId && s.Status == ScreenSessionStatus.CommandSent)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, ScreenSessionStatus.Streaming)
                .SetProperty(s => s.StartedUtc, DateTime.UtcNow), cancellationToken));

    /// <summary> Oturumu kapatır (<c>Stopped</c> ya da <c>Failed</c>) ve açık izleme kayıtlarını bitirir. Kapalı oturuma dokunmaz. </summary>
    public Task CloseSessionAsync(Guid sessionId, ScreenSessionStatus status, ScreenStopReason reason, string? failureReason, CancellationToken cancellationToken = default) =>
        WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            await db.ScreenSessions
                .Where(s => s.Id == sessionId && s.StoppedUtc == null)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Status, status)
                    .SetProperty(s => s.StoppedUtc, now)
                    .SetProperty(s => s.StopReason, reason)
                    .SetProperty(s => s.FailureReason, failureReason), cancellationToken);
            await db.ScreenViewLogs
                .Where(v => v.ScreenSessionId == sessionId && v.EndedUtc == null)
                .ExecuteUpdateAsync(u => u.SetProperty(v => v.EndedUtc, now), cancellationToken);
        });

    public async Task<long> StartViewAsync(Guid sessionId, Guid deviceId, int monitorIndex, Guid userId, CancellationToken cancellationToken = default)
    {
        long id = 0;
        await WithDbAsync(async db =>
        {
            var log = new ScreenViewLog
            {
                ScreenSessionId = sessionId,
                DeviceId = deviceId,
                MonitorIndex = monitorIndex,
                UserId = userId,
                StartedUtc = DateTime.UtcNow
            };
            db.ScreenViewLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
            id = log.Id;
        });
        return id;
    }

    public Task EndViewAsync(long viewLogId, CancellationToken cancellationToken = default) =>
        WithDbAsync(db => db.ScreenViewLogs
            .Where(v => v.Id == viewLogId && v.EndedUtc == null)
            .ExecuteUpdateAsync(u => u.SetProperty(v => v.EndedUtc, DateTime.UtcNow), cancellationToken));

    /// <summary>
    /// Açılışta: önceki süreçten açık kalan oturumlar kapatılır. Canlı durum bellekte olduğu için yeniden başlayan sunucu onları
    /// sürdüremez; istemciler de hub bağlantısı kopunca yayınlarını zaten durdurur.
    /// </summary>
    public async Task<int> CloseOrphansAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        await WithDbAsync(async db =>
        {
            count = await db.ScreenSessions
                .Where(s => s.StoppedUtc == null)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Status, ScreenSessionStatus.Failed)
                    .SetProperty(s => s.StoppedUtc, DateTime.UtcNow)
                    .SetProperty(s => s.StopReason, ScreenStopReason.ServerRestart), cancellationToken);
            // Açılışta canlı izleme olamaz: açık kalmış izleme kayıtları da (oturumu kapalı olanlar dahil) kapanır.
            await db.ScreenViewLogs
                .Where(v => v.EndedUtc == null)
                .ExecuteUpdateAsync(u => u.SetProperty(v => v.EndedUtc, DateTime.UtcNow), cancellationToken);
        });
        return count;
    }

    private async Task WithDbAsync(Func<RemoteDeskDbContext, Task> work)
    {
        using var scope = _scopes.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<RemoteDeskDbContext>());
    }
}
