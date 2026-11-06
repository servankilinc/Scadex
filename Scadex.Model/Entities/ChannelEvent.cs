using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

/// <summary>
/// Bir kanalda gerçekleşmiş, tek bir değer değişimi. Giriş/analog kanalda her değişim; çıkışta başarılı komutun değiştirdiği değer
/// (<see cref="ChannelChangeSource.Command"/>, 2026-10-31) ve karttan okumanın son komuttan farklı bulduğu değer
/// (<see cref="ChannelChangeSource.Sync"/>, "sapma"). Komutun kendisinin kaydı (kim, aç/kapat, NO/NC, başarısızlar) <c>DeviceCommand</c>'dadır;
/// buradaki satır onun kanal üzerindeki sonucudur.
/// </summary>
public class ChannelEvent : IEntity
{
    public long Id { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid AutomationId { get; set; }
    public string Value { get; set; } = null!;
    public string? PreviousValue { get; set; }

    /// <summary> Olayın sahada gerçekleştiği an. <see cref="Source"/> <c>Sync</c> ise yalnızca fark edildiği an (gerçek an bilinmez). </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary> Bilginin ulaştığı an. </summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary> Değişimin kaynağı: kartın bildirimi (<c>Ingest</c>), Scadex'in başarılı komutu (<c>Command</c>) ya da karttan okuma (<c>Sync</c>). </summary>
    public ChannelChangeSource Source { get; set; }

    #region *** EF Core Navigation ***
    public virtual IoChannel? IoChannel { get; set; }
    public virtual Automation? Automation { get; set; }
    #endregion
}
