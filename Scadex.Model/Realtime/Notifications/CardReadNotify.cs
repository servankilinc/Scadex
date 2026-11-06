using Scadex.Core.Model;

namespace Scadex.Model.Realtime.Notifications;

/// <summary> Kontrol modülüne bağlı kart okuyucuda kart okutuldu (<c>POST /api/Scada/card</c>) okunan kartı bu yayından gösterir. Kart tanımsız da olsa yayınlanır </summary>
public class CardReadNotify : IDto
{
    public Guid AutomationId { get; set; }

    /// <summary> SCADA'nın gönderdiği ham kart numarası. </summary>
    public string CardId { get; set; } = null!;

    /// <summary> Kart aktif bir kullanıcıya tanımlıysa sahibi; değilse <c>null</c>. </summary>
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }

    /// <summary> SCADA'nın bildirdiği okuma anı (göndermediyse bize ulaştığı an). </summary>
    public DateTime OccurredAtUtc { get; set; }
}
