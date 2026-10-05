using Scadex.Core.Model;
using Scadex.Core.Utils.Pagination;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary>
/// Denetim raporunun ortak filtresi (izleme listesi, kontrol listesi, özet). Tarih aralığı kaydın BAŞLANGIÇ anına uygulanır —
/// tablolardaki <c>(DeviceId, StartedUtc)</c> / <c>(UserId, StartedUtc)</c> indeksleriyle aynı eksen.
/// </summary>
public class RemoteAccessReportQuery : IDto
{
    public Guid? DeviceId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    /// <summary> Sayfa boyutu 1–100'e kırpılır: denetim tablosu sınırsız büyür (saklama işi yok). </summary>
    public PaginationRequest ToPaginationRequest() => new() { Page = Math.Max(Page, 1), PageSize = Math.Clamp(PageSize, 1, 100) };
}
