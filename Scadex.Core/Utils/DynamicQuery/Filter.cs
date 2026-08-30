namespace Scadex.Core.Utils.DynamicQuery;

public class Filter
{
    public string? Field { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public string Logic { get; set; } = "and";
    public List<Filter>? Filters { get; set; }
}
