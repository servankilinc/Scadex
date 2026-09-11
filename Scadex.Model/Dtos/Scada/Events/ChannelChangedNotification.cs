using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Events;

/// <summary> Bir kanalın değeri değiştiğinde ve db ye yazıldığında Observer Pattern ile Gozlemcilere (<c>IScadaEventObserver</c>) ile yayınlanır. </summary>
public class ChannelChangedNotification : IDto
{
    public Guid CabinetId { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid DeviceId { get; set; }
    public PinDirection Direction { get; set; }
    public int ChannelNumber { get; set; }
    public string? Value { get; set; }
    public string? PreviousValue { get; set; }

    /// <summary> Sahada gerceklestigi an (SCADA gondermediyse bize ulaştığı an). </summary>
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
