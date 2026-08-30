namespace Scadex.Core.Utils.Datatable;

public class DatatableResponseServerSide<TData>
{
    public int Draw { get; set; }
    public int RecordsTotal { get; set; }
    public int RecordsFiltered { get; set; }
    public List<TData>? Data { get; set; }
}