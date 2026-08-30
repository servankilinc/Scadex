using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.DynamicQuery;

namespace Scadex.Core.BaseRequestModels;

public class DynamicDatatableRequest : DatatableRequest
{
    public Filter? Filter { get; set; }
    public IEnumerable<Sort>? Sorts { get; set; }
}