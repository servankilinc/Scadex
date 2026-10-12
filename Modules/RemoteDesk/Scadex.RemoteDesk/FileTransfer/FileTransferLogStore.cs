using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scadex.RemoteDesk.DataAccess;
using Scadex.RemoteDesk.Model.Entities;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.FileTransfer;

/// <summary>
/// <c>FileTransferLog</c> yazımı — tablonun TEK yazım yolu. Koordinatör singleton olduğu için her işlem kendi scope'unda yapılır
/// (<see cref="Control.RemoteControlStore"/> ile aynı kalıp).
/// </summary>
public sealed class FileTransferLogStore
{
    private readonly IServiceScopeFactory _scopes;

    public FileTransferLogStore(IServiceScopeFactory scopes) => _scopes = scopes;

    public Task StartAsync(Guid id, Guid deviceId, Guid userId, FileTransferDirection direction, string fileName, string path, CancellationToken cancellationToken = default) =>
        WithDbAsync(async db =>
        {
            db.FileTransferLogs.Add(new FileTransferLog
            {
                Id = id,
                DeviceId = deviceId,
                UserId = userId,
                Direction = direction,
                FileName = Trim(fileName, 260),
                Path = Trim(path, 1024),
                StartedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        });

    /// <summary> Açık kayda dokunur; kapalıysa (zaten bitmiş) etkisizdir. </summary>
    public Task EndAsync(Guid id, bool success, long bytes, string? error, CancellationToken cancellationToken = default) =>
        WithDbAsync(db => db.FileTransferLogs
            .Where(f => f.Id == id && f.EndedUtc == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(f => f.EndedUtc, DateTime.UtcNow)
                .SetProperty(f => f.Success, success)
                .SetProperty(f => f.SizeBytes, bytes)
                .SetProperty(f => f.Error, error == null ? null : Trim(error, 512)), cancellationToken));

    /// <summary> Açılışta: önceki süreçten açık kalan transfer kayıtları başarısız olarak kapatılır. </summary>
    public async Task<int> CloseOrphansAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        await WithDbAsync(async db =>
        {
            count = await db.FileTransferLogs
                .Where(f => f.EndedUtc == null)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(f => f.EndedUtc, DateTime.UtcNow)
                    .SetProperty(f => f.Success, false)
                    .SetProperty(f => f.Error, "Sunucu yeniden başladı"), cancellationToken);
        });
        return count;
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    private async Task WithDbAsync(Func<RemoteDeskDbContext, Task> work)
    {
        using var scope = _scopes.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<RemoteDeskDbContext>());
    }
}
