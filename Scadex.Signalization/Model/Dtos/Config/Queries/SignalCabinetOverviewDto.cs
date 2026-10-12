using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Config.Queries;

/// <summary> Sinyalizasyon kabin tablosunun bir satırı, Kabin genel bilgileri (firma, konum, scadex çekirdeği durumu) bilerek YOK: onlar cekirdegin Kabinler raporunda </summary>
public class SignalCabinetOverviewDto : IDto
{
    public Guid CabinetId { get; set; }
    public string CabinetName { get; set; } = null!;
    /// <summary> Cekirdekteki <c>Cabinet.IsActive</c>. </summary>
    public bool CabinetIsActive { get; set; }

    /// <summary> Modulde yapilandirma satiri var mi. <c>false</c> ise asagidaki yapilandirma alanlari bos / <c>null</c>dir. </summary>
    public bool IsConfigured { get; set; }
    /// <summary> <c>false</c> ise motor bu kabinin olaylarini yok sayar. </summary>
    public bool IsEnabled { get; set; }

    #region Yapilandirma ozeti (yalnizca AKTIF kapilar)
    public int OuterDoorCount { get; set; }
    public int InnerDoorCount { get; set; }
    /// <summary> Kamerasi tanimli dis kapi sayisi. </summary>
    public int CameraCount { get; set; }
    /// <summary> Ic kapilarin kurumlari: tekil, ada gore sirali. </summary>
    public List<string> AuthorityNames { get; set; } = [];
    public bool HasSiren { get; set; }
    /// <summary> Siren caliyor mu; siren tanimsiz ya da hic basarili komut gormemisse <c>null</c> (bilinmiyor). </summary>
    public bool? SirenIsOn { get; set; }
    #endregion

    #region Zamanlama ayarlari (yapilandirilmamis kabinde null — varsayilanlar ayar gibi gosterilmesin)
    public int? SirenDurationSec { get; set; }
    public int? AwaitingCardTimeoutSec { get; set; }
    public int? SessionMaxDurationMin { get; set; }
    public int? EntrySnapshotCount { get; set; }
    public int? EntrySnapshotIntervalMs { get; set; }
    #endregion

    #region Islem istatistigi
    /// <summary> Kabinde baslamis en son islemin baslangici (acik islem dahil). </summary>
    public DateTime? LastSessionStartedAtUtc { get; set; }
    /// <summary> Son 30 gunde BASLAMIS islem sayisi (acik islem dahil). </summary>
    public int SessionCount30d { get; set; }
    /// <summary> Bunlardan herhangi bir bayragi olanlar — rapordaki "Bayrakli" ile ayni anlam. </summary>
    public int FlaggedSessionCount30d { get; set; }
    #endregion
}
