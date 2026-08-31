using Scadex.Core.Model;

namespace Scadex.Model.Entities;

/// <summary> Bir Input kanalında gerçekleşmiş, tek bir değer değişimi. </summary>
public class ChannelEvent : IEntity
{
    public long Id { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid CabinetId { get; set; }
    public string Value { get; set; } = null!;
    public string? PreviousValue { get; set; }

    /// <summary> Olayın sahada gerçekleştiği an. </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary> Bilginin ulaştığı an. </summary>
    public DateTime ReceivedAtUtc { get; set; }

    #region *** EF Core Navigation ***
    public virtual IoChannel? IoChannel { get; set; }
    public virtual Cabinet? Cabinet { get; set; }
    #endregion
}
