using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scadex.Core.Model;
using Scadex.Core.Utils.HttpContextManager;

namespace Scadex.DataAccess.Interceptors;

/// <summary>
/// 1. (IImmutableEntity): Throws exception on Update/Delete.
/// 2. (IActivatableEntity): Throws exception on Delete (must use IsActive = false instead).
/// 3. (ISoftDeletableEntity): Converts physical deletes into soft deletes.
/// </summary>
public sealed class EntityLifecycleInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextManager _httpContextManager;
    public EntityLifecycleInterceptor(IHttpContextManager httpContextManager) => _httpContextManager = httpContextManager;

    #region SYNC VERSION
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return base.SavingChanges(eventData, result);

        ProcessLifecycle(eventData.Context);

        return base.SavingChanges(eventData, result);
    }
    #endregion

    #region ASYNC VERSION
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        ProcessLifecycle(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    #endregion

    private void ProcessLifecycle(DbContext context)
    {
        var entries = context.ChangeTracker.Entries().ToList();

        // 1. Validate Immutability
        var immutableEntries = entries.Where(e => e.Entity is IImmutableEntity && e.Entity is not IProjectEntity && (e.State == EntityState.Modified || e.State == EntityState.Deleted));
        foreach (var entry in immutableEntries)
        {
            throw new InvalidOperationException($"Entity of type '{entry.Metadata.Name}' with ID '{entry.Property("Id").CurrentValue ?? "unknown"}' is immutable (System Locked) and cannot be updated or deleted.");
        }

        // 2. Validate Activatable
        var activatableEntries = entries.Where(e => e.Entity is IActivatableEntity && e.Entity is not IProjectEntity && e.State == EntityState.Deleted);
        foreach (var entry in activatableEntries)
        {
            throw new InvalidOperationException($"Entity of type '{entry.Metadata.Name}' with ID '{entry.Property("Id").CurrentValue ?? "unknown"}' cannot be deleted. It can only be deactivated by setting IsActive = false.");
        }

        // 3. Apply Soft Delete
        var softDeletableEntries = entries.Where(e => e.Entity is ISoftDeletableEntity && e.Entity is not IProjectEntity && e.State == EntityState.Deleted);

        if (softDeletableEntries.Any())
        {
            var requesterId = _httpContextManager.GetNameIdentifier();
            string deletedBy = requesterId.IsSuccess ? requesterId.Data : string.Empty;
            DateTime now = DateTime.UtcNow;

            foreach (var entry in softDeletableEntries)
            {
                var softDeletable = (ISoftDeletableEntity)entry.Entity;
                entry.State = EntityState.Modified;
                softDeletable.DeletedBy = deletedBy;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedDateUtc = now;
            }
        }
    }
}