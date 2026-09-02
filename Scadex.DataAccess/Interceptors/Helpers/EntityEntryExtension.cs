using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using Scadex.Core.Enums;
using Scadex.Core.Utils.CriticalData;

namespace Scadex.DataAccess.Interceptors.Helpers;

public static class EntityEntryExtension
{
    public static string? GetTableName(this EntityEntry entry)
    {
        string? tableName = default;
        if (entry.Entity != null)
        {
            tableName = entry.Entity.GetType().Name;
        }
        return tableName;
    }

    public static string? GetEntityId(this EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey == null) return string.Empty;

        var keyValues = new List<string>();

        foreach (var property in primaryKey.Properties)
        {
            var value = entry.Property(property.Name).CurrentValue;

            if (value == null) return string.Empty;

            keyValues.Add(value.ToString()!);
        }

        // Sadece PK var direkt dön
        if (keyValues.Count == 1)
            return keyValues[0];

        return string.Join("-", keyValues);
    }

    public static CrudType GetActionType(this EntityEntry entry)
    {
        CrudType actionType = CrudType.Undefined;
        switch (entry.State)
        {
            case EntityState.Added:
                actionType = CrudType.Create;
                break;
            case EntityState.Modified:
                actionType = CrudType.Update;
                break;
            case EntityState.Deleted:
                actionType = CrudType.Delete;
                break;
        }
        return actionType;
    }

    public static string? GetOriginalData(this EntityEntry entry)
    {
        string? data = string.Empty;
        if (entry.OriginalValues != null)
        {
            data = JsonConvert.SerializeObject(entry.OriginalValues.ToObject(), new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                MaxDepth = 7,
                ContractResolver = new IgnoreCriticalDataResolver()
            });
        }
        return data;
    }

    public static string? GetCurrentData(this EntityEntry entry)
    {
        string? data = string.Empty;
        if (entry.Entity != null)
        {
            data = JsonConvert.SerializeObject(entry.CurrentValues.ToObject(), new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                MaxDepth = 7,
                ContractResolver = new IgnoreCriticalDataResolver()
            });
        }
        return data;
    }
}
