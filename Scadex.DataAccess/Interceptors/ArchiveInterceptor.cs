using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scadex.Core.Model;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.DataAccess.Interceptors.Helpers;
using Scadex.Model.ProjectEntities;

namespace Scadex.DataAccess.Interceptors;

public sealed class ArchiveInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextManager _httpContextManager;
    public ArchiveInterceptor(IHttpContextManager httpContextManager) => _httpContextManager = httpContextManager;

    #region SYNC VERSION
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return base.SavingChanges(eventData, result);

        ProcessArchive(eventData.Context);

        return base.SavingChanges(eventData, result);
    }
    #endregion

    #region ASYNC VERSION
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        ProcessArchive(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    #endregion

    private void ProcessArchive(DbContext context)
    {
        IEnumerable<EntityEntry<IArchivableEntity>> archivableEntries = context.ChangeTracker.Entries<IArchivableEntity>()
            .Where(e => (e.State == EntityState.Modified || e.State == EntityState.Deleted) && e.Entity is not IProjectEntity);

        if (archivableEntries.Any())
        {
            List<Archive> archives = new List<Archive>();

            var requesterId = _httpContextManager.GetNameIdentifier();
            var clientIp = _httpContextManager.GetClientIp();
            var userAgent = _httpContextManager.GetUserAgent();

            foreach (EntityEntry<IArchivableEntity> entry in archivableEntries)
            {
                archives.Add(new Archive
                {
                    TableName = entry.GetTableName(),
                    EntityId = entry.GetEntityId(),
                    RequesterId = requesterId.IsSuccess ? requesterId.Data : string.Empty,
                    ClientIp = clientIp.IsSuccess ? clientIp.Data : string.Empty,
                    UserAgent = userAgent.IsSuccess ? userAgent.Data : string.Empty,
                    Action = entry.GetActionType(),
                    DateUtc = DateTime.UtcNow,
                    Data = entry.GetOriginalData()
                });
            }
            context.Set<Archive>().AddRange(archives);
        }
    }
}
