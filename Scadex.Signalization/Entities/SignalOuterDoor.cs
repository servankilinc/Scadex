using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// SANAL dis kapi: cekirdekte kapi entity'si yoktur ve eklenmez. Bu kayit bir anahtar (switch) kanalini ve dis kapi ile
/// ic kapilar arasindaki kamerayi adla eslestirir. Iliski <c>Device</c>'a degil <c>IoChannel</c>'a kurulur: Device bir kartin
/// tamamidir, anahtarlar ayni giris kartinin farkli kanallarinda durabilir.
/// Fiziksel silme yoktur (oturum olaylari kimligi gosterir); yapilandirmadan cikan kapi pasife alinir.
/// </summary>
public class SignalOuterDoor : IEntity, IAuditableEntity, IActivatableEntity
{
    /// <summary> Istemci uretir (yapilandirma agaci tek gonderide kaydedilir). </summary>
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary> Dis kapi anahtarinin giris kanali (<c>IoChannel</c>, <c>Input</c>) — FK DEGIL. </summary>
    public Guid SwitchIoChannelId { get; set; }

    /// <summary> Anahtar kanalinin "kapi acik" anlamina gelen ham degeri (kanal degeri tipsiz string'dir). </summary>
    public string SwitchOpenValue { get; set; } = "1";

    /// <summary> Dis kapi acildiginda kare cekilecek kamera (cekirdekteki <c>Camera.Id</c>) — FK DEGIL. </summary>
    public Guid? CameraId { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual SignalCabinet? Cabinet { get; set; }
    public virtual ICollection<SignalInnerDoor>? InnerDoors { get; set; }
    #endregion
}
