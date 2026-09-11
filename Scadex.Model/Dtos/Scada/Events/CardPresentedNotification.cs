using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Scada.Events;

/// <summary> Kabindeki bir kart okuyucuya kart okutuldu. </summary>
public class CardPresentedNotification : IDto
{
    public Guid CabinetId { get; set; }
    public string CardIdRaw { get; set; } = null!;

    /// <summary> Kartın sahibi olan kullanıcı; kart tanınmazsa null. </summary>
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }

    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
