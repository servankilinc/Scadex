using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.ChannelEvent.Queries;

public class ChannelEventDto : IDto
{
    public long Id { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid CabinetId { get; set; }
    public string? ChannelName { get; set; }
    public int? ChannelNumber { get; set; }

    /// <summary> Kanalın yönü: giriş ve çıkış numaraları çakışır (IN5 / OUT5), adres ancak yönle birlikte okunur. </summary>
    public PinDirection? Direction { get; set; }

    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }

    public string Value { get; set; } = null!;
    public string? PreviousValue { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary> <c>Ingest</c> = kartın bildirimi, <c>Sync</c> = karttan okumanın bulduğu fark (çıkışta: son komuttan sapma). </summary>
    public ChannelChangeSource Source { get; set; }
}
