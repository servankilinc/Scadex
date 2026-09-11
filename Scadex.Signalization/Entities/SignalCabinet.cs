using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Bir kabinin sinyalizasyon yapilandirmasi. Satir yoksa ya da <see cref="IsEnabled"/> kapaliysa modul o kabinin
/// olaylarini YOK SAYAR — "bu kabin bu musteriye mi ait" sorusu kodda degil, bu satirin varliginda cevaplanir.
/// Tek yazim yolu: <c>PUT /api/SignalCabinet/{cabinetId}</c> (tum agac).
/// </summary>
public class SignalCabinet : IEntity, IAuditableEntity
{
    /// <summary> Cekirdekteki <c>Cabinet.Id</c> — FK DEGIL (farkli context). </summary>
    public Guid CabinetId { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Kabinin TEK ve ORTAK sireni: role kartindaki bir cikis kanali (<c>IoChannel</c>, <c>Output</c>).
    /// Siren bir kapinin degil kabinin ozelligidir; her dis kapi oturumu yalnizca bir siren TALEBI tasir.
    /// </summary>
    public Guid? SirenIoChannelId { get; set; }
    public int SirenDurationSec { get; set; } = 120;

    public int EntrySnapshotCount { get; set; } = 5;
    public int EntrySnapshotIntervalMs { get; set; } = 1000;

    /// <summary> Dis kapi acildiktan sonra yetkili kart icin beklenen sure; asilirsa oturum "yetkisiz mudahale" isaretlenir. 0 = kapali. </summary>
    public int AwaitingCardTimeoutSec { get; set; } = 120;

    /// <summary> Dis kapi kapanmazsa oturumun zamanlayiciyla kapatilacagi sure. </summary>
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
