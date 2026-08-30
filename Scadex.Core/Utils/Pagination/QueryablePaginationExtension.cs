using Microsoft.EntityFrameworkCore;

namespace Scadex.Core.Utils.Pagination;

public static class QueryablePaginationExtension
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 1000;

    public static PaginationResponse<TData> ToPaginate<TData>(this IQueryable<TData> queryable, PaginationRequest request)
    {
        int count = queryable.Count();

        if (request.Page == default || request.Page <= 0)
            request.Page = 1;

        if (request.PageSize == default || request.PageSize <= 0)
            request.PageSize = DefaultPageSize;

        if (request.PageSize > MaxPageSize)
            request.PageSize = MaxPageSize;

        List<TData> items = queryable.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        PaginationResponse<TData> list = new()
        {
            Page = request.Page,
            PageSize = request.PageSize,
            DataCount = count,
            Data = items,
            PageCount = (int)Math.Ceiling(count / (double)request.PageSize)
        };
        return list;
    }

    public static async Task<PaginationResponse<TData>> ToPaginateAsync<TData>(this IQueryable<TData> queryable, PaginationRequest request, CancellationToken cancellationToken = default)
    {
        int count = await queryable.CountAsync(cancellationToken);

        if (request.Page == default || request.Page <= 0)
            request.Page = 1;

        if (request.PageSize == default || request.PageSize <= 0)
            request.PageSize = DefaultPageSize;

        if (request.PageSize > MaxPageSize)
            request.PageSize = MaxPageSize;

        List<TData> items = await queryable.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PaginationResponse<TData>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            DataCount = count,
            Data = items,
            PageCount = (int)Math.Ceiling(count / (double)request.PageSize)
        };
    }
}
