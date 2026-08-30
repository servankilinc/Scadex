using Scadex.Core.Utils.DynamicQuery;
using Scadex.Core.Utils.Pagination;

namespace Scadex.Core.BaseRequestModels;

public class DynamicPaginationRequest : PaginationRequest
{
    public Filter? Filter { get; set; }
    public IEnumerable<Sort>? Sorts { get; set; }
}
