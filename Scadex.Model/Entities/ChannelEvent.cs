using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

/// <summary>
/// Bir kanalda gerçekleşmiş, tek bir değer değişimi. Giriş/analog kanalda her değişim; çıkışta YALNIZCA karttan okumanın son komuttan
/// farklı bulduğu değer (<see cref="ChannelChangeSource.Sync"/>, "sapma") — komutların kaydı <c>DeviceCommand</c>'dadır.
/// </summary>
public class ChannelEvent : IEntity
{
    public long Id { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid CabinetId { get; set; }
    public string Value { get; set; } = null!;
    public string? PreviousValue { get; set; }

    /// <summary> Olayın sahada gerçekleştiği an. <see cref="Source"/> <c>Sync</c> ise yalnızca fark edildiği an (gerçek an bilinmez). </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary> Bilginin ulaştığı an. </summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary> Değişimi kim fark etti: kartın bildirimi (<c>Ingest</c>) ya da karttan okuma (<c>Sync</c>). <c>Command</c> buraya yazılmaz. </summary>
    public ChannelChangeSource Source { get; set; }

    #region *** EF Core Navigation ***
    public virtual IoChannel? IoChannel { get; set; }
    public virtual Cabinet? Cabinet { get; set; }
    #endregion
}
