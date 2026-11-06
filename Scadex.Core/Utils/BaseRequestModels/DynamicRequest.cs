using Scadex.Core.Utils.DynamicQuery;

namespace Scadex.Core.Utils.BaseRequestModels;

public class DynamicRequest
{
    public Filter? Filter { get; set; }
    public IEnumerable<Sort>? Sorts { get; set; }
}
