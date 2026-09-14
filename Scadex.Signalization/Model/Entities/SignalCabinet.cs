using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary> Bir kabinin sinyalizasyon yapılandirmasi. "bu kabin bu musteriye mi ait" sorusu kodda degil, bu satirin varliginda cevaplanir. </summary>
public class SignalCabinet : IEntity, IAuditableEntity
{
    public Guid CabinetId { get; set; }

    /// <summary> Bilgi yoksa ya da<see cref="IsEnabled"/> false ise kabinin eventleri yok sayılır <summary>
    public bool IsEnabled { get; set; }

    #region Siren Ayarları
    /// <summary> Kabinin sireni: output kanalı <c>IoChannel</c>. </summary>
    public Guid? SirenIoChannelId { get; set; }
    public int SirenDurationSec { get; set; } = 120;
    #endregion

    #region Görüntü Yakalama Ayarları
    /// <summary> Dış kapı açılınca kaç adet görüntü yakalanacak. </summary>
    public int EntrySnapshotCount { get; set; } = 5;
    /// <summary> Dış kapı açılınca kaç adet görüntü kaç sn aralıkla yakalanacak. </summary>
    public int EntrySnapshotIntervalMs { get; set; } = 1000;
    #endregion

    /// <summary> Dış kapı açıldıktan sonra yetkili kartı için beklenen sure; aşılırsa oturum "yetkisiz müdahale" işaretlenir. "0" ise kontrol yapılmaz. </summary>
    public int AwaitingCardTimeoutSec { get; set; } = 120;

    /// <summary> Dış kapı kapanmazsa(iç kapı kilitlendikten sonra) oturumun otomatik kapatılacağı dk. Varsayılan 4 saat </summary>
    public int SessionMaxDurationMin { get; set; } = 240;

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual ICollection<SignalOuterDoor>? OuterDoors { get; set; }
    #endregion
}
