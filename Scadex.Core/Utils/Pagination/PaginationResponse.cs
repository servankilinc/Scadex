namespace Scadex.Core.Utils.Pagination;

public class PaginationResponse<TData>
{
    public PaginationResponse()
    {
        Data = Array.Empty<TData>();
    }

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int DataCount { get; set; }
    public int PageCount { get; set; }
    public IList<TData> Data { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page + 1 < PageCount;
}
