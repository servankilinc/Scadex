using Scadex.Core.Model;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Bir DIS KAPININ bir acilis-kapanis dongusu: operator islemi. Dis kapi basina ayni anda tek acik oturum olur
/// (<c>IX_OperatorSession_OuterDoorId</c>, unique, WHERE EndedAtUtc IS NULL); iki dis kapi bagimsiz oturumlar tasir.
/// Yalnizca motor yazar; HTTP yazim yolu yoktur. Uyari bayraklari (<see cref="Flags"/>) kalicidir — onay akisi yoktur,
/// rapor <c>flags</c> filtresiyle bulur. Arsivlenmez — raporun kaynagidir.
/// </summary>
public class OperatorSession : IEntity
{
    public long Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid OuterDoorId { get; set; }

    /// <summary> Kapi sonradan yeniden adlandirilsa da rapor O GUNKU adi gostermeli — bilincli enstantane. </summary>
    public string OuterDoorNameSnapshot { get; set; } = null!;

    public OperatorSessionStatus Status { get; set; }
    public SessionFlags Flags { get; set; }

    /// <summary> Dis kapinin acildigi an (sahadaki zaman). Sayac burada baslar. </summary>
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSec { get; set; }

    /// <summary> Bu ana kadar yetkili kart okutulmazsa oturum "yetkisiz mudahale" isaretlenir; ilk yetkili kartta null'lanir. </summary>
    public DateTime? AwaitingCardDueAtUtc { get; set; }

    /// <summary> Dis kapi bu ana kadar kapanmazsa oturum zamanlayiciyla kapatilir. </summary>
    public DateTime? MaxDurationDueAtUtc { get; set; }

    #region --- Siren talebi (fiziksel durum SignalCabinetState'te) ---
    /// <summary> Oturumun siren talebinin acildigi an; talep acik = dolu ve <see cref="SirenReleasedAtUtc"/> bos. </summary>
    public DateTime? SirenRequestedAtUtc { get; set; }
    public DateTime? SirenOffDueAtUtc { get; set; }
    public DateTime? SirenReleasedAtUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual ICollection<OperatorSessionOperator>? Operators { get; set; }
    public virtual ICollection<OperatorSessionEvent>? Events { get; set; }
    public virtual ICollection<OperatorSessionCapture>? Captures { get; set; }
    #endregion

    /// <summary> Siren talebi su an acik mi? </summary>
    public bool HasActiveSirenRequest => SirenRequestedAtUtc != null && SirenReleasedAtUtc == null;
}
