using Scadex.Core.Model;

namespace Scadex.Model.Realtime.Events;

/// <summary> Otomasyondaki bir kart okuyucuya kart okutuldu. </summary>
public class CardPresentedEvent : IDto
{
    public Guid AutomationId { get; set; }
    public string CardIdRaw { get; set; } = null!;

    /// <summary> Kartın sahibi olan kullanıcı; kart tanınmazsa null. </summary>
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }

    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
