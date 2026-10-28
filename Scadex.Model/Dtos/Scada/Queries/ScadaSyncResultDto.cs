using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Scada.Queries;

/// <summary> Karttan okuma (<c>GET /status</c>) ile kabinin kanallarını senkronlamanın özeti. Değişen değerler ayrıca <c>DiagramHub</c>'dan yayınlanır. </summary>
public class ScadaSyncResultDto : IDto
{
    /// <summary> Kartın bildirdiği kanal sayısı. </summary>
    public int ReadCount { get; set; }

    /// <summary> Kayıtlı değerinden farklı çıkıp yazılan kanal sayısı. </summary>
    public int ChangedCount { get; set; }

    /// <summary> Kartın bildirdiği ama bu kabinde tanımlı (etkin) karşılığı olmayan ya da tipi tanınmayan kanal sayısı. </summary>
    public int UnmatchedCount { get; set; }

    public DateTime SyncedAtUtc { get; set; }
}
