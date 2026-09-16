using Scadex.Core.Model;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Model.Entities;

public class OperatorSession : IEntity
{
    public long Id { get; set; }
    public Guid CabinetId { get; set; }

    /// <summary> İşlemi başlatan dış kapı </summary>
    public Guid OuterDoorId { get; set; }
    public string OuterDoorNameSnapshot { get; set; } = null!;

    public OperatorSessionStatus Status { get; set; }
    public SessionFlags Flags { get; set; }

    /// <summary> Dış kapının açıldığı an, Sayaç burada başlar. </summary>
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSec { get; set; }


    /// <summary> Bu ana kadar yetkili kartını okutumazsa oturum "yetkisiz mudahale" isaretlenir; ilk yetkili kartını okuttuğunda null'lanir. </summary>
    public DateTime? AwaitingCardDueAtUtc { get; set; }

    /// <summary> Dış kapı bu ana kadar kapanmazsa oturum zamanlayıcıyla "SginalCabinet.SessionMaxDurationMin" kadar bekleyip kapatılır. </summary>
    public DateTime? MaxDurationDueAtUtc { get; set; }


    #region --- Siren talebi ---
    /// <summary> Bu oturumun sireni açma talebini actigi an; <see cref="SirenReleasedAtUtc"/> null oldugu surece talep açıktır. </summary>
    public DateTime? SirenRequestedAtUtc { get; set; }

    /// <summary> Zamanlayıcının bu talebi otomatik kapatacagi an (<c>SignalCabinet.SirenDurationSec</c> sonra); </summary>
    public DateTime? SirenOffDueAtUtc { get; set; }

    /// <summary> Sirenin susturulduğu an; null ise talep hala açıktır. </summary>
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
