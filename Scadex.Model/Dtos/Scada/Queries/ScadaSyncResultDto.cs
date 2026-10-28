using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Queries;

/// <summary>
/// Karttan okuma (<c>GET /status</c>) ile kabinin kanallarını senkronlamanın özeti. Değişen değerler ayrıca <c>DiagramHub</c>'dan yayınlanır.
/// Komut sonucuyla aynı sözleşme: kartın kendisinden gelen başarısızlık (<see cref="CommandStatus.Failed"/> / <see cref="CommandStatus.NoResponse"/>)
/// HATA DEĞİLDİR, <see cref="Status"/>'te döner — izleme turu "kart cevap verdi ama okuyamadı" ile "kart yok"u bu sayede ayırır.
/// </summary>
public class ScadaSyncResultDto : IDto
{
    /// <summary> Okumanın sonucu. Yalnızca <see cref="CommandStatus.Succeeded"/>'te sayılar anlamlıdır. </summary>
    public CommandStatus Status { get; set; }

    /// <summary> Başarısızlıkta kartın / bağlantının mesajı. </summary>
    public string? Message { get; set; }

    /// <summary> Kartın bildirdiği kanal sayısı. </summary>
    public int ReadCount { get; set; }

    /// <summary> Kayıtlı değerinden farklı çıkıp yazılan kanal sayısı. </summary>
    public int ChangedCount { get; set; }

    /// <summary> Kartın bildirdiği ama bu kabinde tanımlı (etkin) karşılığı olmayan ya da tipi tanınmayan kanal sayısı. </summary>
    public int UnmatchedCount { get; set; }

    public DateTime SyncedAtUtc { get; set; }
}
