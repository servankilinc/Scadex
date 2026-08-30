using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace Scadex.Core.Utils.Datatable;

public static class QueryableDatatableExtension
{
    public const int MaxPageSize = 5000;

    #region SERVER-SIDE VERSION
    public static DatatableResponseServerSide<TData> ToDatatableServerSide<TData>(this IQueryable<TData> query, DatatableRequest dataTableRequest)
    {
        // 1. Count of Total Records
        int recordsTotal = query.Count();

        // 2. Filter by search parameter
        string? searchPredicate = GenerateSearchPredicate<TData>(dataTableRequest);
        if (!string.IsNullOrWhiteSpace(searchPredicate)) query = query.Where(searchPredicate, dataTableRequest.Search!.Value!.ToLower());

        // 3. Count of Filtered Records
        int recordsFiltered = query.Count();

        // 4. Ordering 
        string? orderPredicate = GenerateOrderPredicate<TData>(dataTableRequest);
        if (!string.IsNullOrWhiteSpace(orderPredicate)) query = query.OrderBy(orderPredicate);

        // 5. Pagination
        // DataTables sends Length = -1 for its "All" option, and Start can arrive negative.
        // Clamp both, and cap the page size so one request cannot pull the whole table.
        int skip = Math.Max(0, dataTableRequest.Start);
        int take = dataTableRequest.Length <= 0 ? MaxPageSize : Math.Min(dataTableRequest.Length, MaxPageSize);
        query = query.Skip(skip).Take(take);

        var data = query.ToList();

        return new DatatableResponseServerSide<TData>
        {
            Data = data,
            Draw = dataTableRequest.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsFiltered,
        };
    }

    public static async Task<DatatableResponseServerSide<TData>> ToDatatableServerSideAsync<TData>(this IQueryable<TData> query, DatatableRequest dataTableRequest, CancellationToken cancellationToken = default)
    {
        // 1. Count of Total Records
        int recordsTotal = await query.CountAsync();

        // 2. Filter by search parameter
        string? searchPredicate = GenerateSearchPredicate<TData>(dataTableRequest);
        if (!string.IsNullOrWhiteSpace(searchPredicate)) query = query.Where(searchPredicate, dataTableRequest.Search!.Value!.ToLower());

        // 3. Count of Filtered Records
        int recordsFiltered = await query.CountAsync();

        // 4. Ordering 
        string? orderPredicate = GenerateOrderPredicate<TData>(dataTableRequest);
        if (!string.IsNullOrWhiteSpace(orderPredicate)) query = query.OrderBy(orderPredicate);

        // 5. Pagination
        // DataTables sends Length = -1 for its "All" option, and Start can arrive negative.
        // Clamp both, and cap the page size so one request cannot pull the whole table.
        int skip = Math.Max(0, dataTableRequest.Start);
        int take = dataTableRequest.Length <= 0 ? MaxPageSize : Math.Min(dataTableRequest.Length, MaxPageSize);
        query = query.Skip(skip).Take(take);

        var data = await query.ToListAsync(cancellationToken);

        return new DatatableResponseServerSide<TData>
        {
            Data = data,
            Draw = dataTableRequest.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsFiltered,
        };
    }
    #endregion

    #region CLIENT-SIDE VERSION
    public static DatatableResponseClientSide<TData> ToDatatableClientSide<TData>(this IQueryable<TData> query)
    {
        return new DatatableResponseClientSide<TData>()
        {
            Data = query.ToList(),
        };
    }

    public static async Task<DatatableResponseClientSide<TData>> ToDatatableClientSideAsync<TData>(this IQueryable<TData> query, CancellationToken cancellationToken = default)
    {
        var data = await query.ToListAsync(cancellationToken);
        return new DatatableResponseClientSide<TData>()
        {
            Data = data,
        };
    }
    #endregion


    #region HELPERS
    private static string? GenerateSearchPredicate<TData>(DatatableRequest dataTableRequest)
    {
        if (dataTableRequest.Search == null || string.IsNullOrWhiteSpace(dataTableRequest.Search.Value) || dataTableRequest.Columns == null) return null;

        IEnumerable<Column>? searchableColumns = dataTableRequest.Columns!.Where(c => c.Searchable && !string.IsNullOrWhiteSpace(c.Data)).ToList();

        var props = typeof(TData)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToDictionary(p => p.ToLower(), p => p);

        foreach (var column in searchableColumns) // c.Data is column name
        {
            var key = column.Data!.ToLower();
            if (props.TryGetValue(key, out var actualPropName))
            {
                column.Data = actualPropName;
            }
            else
            {
                column.Searchable = false;
            }
        }
        var filters = searchableColumns.Where(f => f.Searchable)
            .Select(c => $"({c.Data}.ToLower().StartsWith(@0) OR {c.Data}.ToLower().EndsWith(@0) OR {c.Data}.ToLower().Contains(@0))");

        var searchPredicate = string.Join(" OR ", filters);
        return searchPredicate;
    }

    private static string? GenerateOrderPredicate<TData>(DatatableRequest dataTableRequest)
    {
        if (dataTableRequest.Order == null || dataTableRequest.Columns == null) return null;

        var props = typeof(TData).GetProperties().Select(p => p.Name).ToDictionary(p => p.ToLower(), p => p);

        List<string> orderList = new List<string>();
        foreach (var orderItem in dataTableRequest.Order)
        {
            // The column index comes from the client; an out-of-range value would throw.
            if (orderItem.Column < 0 || orderItem.Column >= dataTableRequest.Columns.Count) continue;

            var column = dataTableRequest.Columns[orderItem.Column];
            if (column == null || !column.Orderable || string.IsNullOrWhiteSpace(column.Data)) continue;

            // Dir is interpolated into the ordering string; anything but asc/desc could inject
            // additional ordering terms (e.g. "asc, PasswordHash desc").
            var dir = string.Equals(orderItem.Dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

            var key = column.Data.ToLower();
            if (props.TryGetValue(key, out var actualPropName))
            {
                orderList.Add($"{actualPropName} {dir}");
            }
        }
        string orderPredicate = string.Join(",", orderList);

        if (orderList.Any()) return orderPredicate;
        return null;
    }
    #endregion
}