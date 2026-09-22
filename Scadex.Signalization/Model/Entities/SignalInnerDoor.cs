using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;


/// <summary>
/// <strong> SANAL İç Kapı: </strong>
/// <list type="bullet">
/// <item> Kapının anahtar (switch) input kanalı <c>IoChannel</c> ile eşleştirir. </item>
/// <item> Kapının kili Output kanalı <c>IoChannel</c> ile eşleştirir. </item>
/// <item> Yekili kurumla eşleştirir. Yetkilendirme zinciri: <c>User → Rol → SignalAuthority → SignalInnerDoor → LockIoChannelId</c> </item>
/// </list>
/// </summary>
public class SignalInnerDoor : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public Guid OuterDoorId { get; set; }
    public string Name { get; set; } = null!;
    public Guid AuthorityId { get; set; }

    /// <summary> İç kapı switch input kanalı (<c>IoChannel</c>). Kapinin fiziksel açılıp kapandığını bu doğrular. </summary>
    public Guid SwitchIoChannelId { get; set; }

    /// <summary> Switch kanalının "kapı açık" anlamına gelen değeri. </summary>
    public string SwitchOpenValue { get; set; } = "1";

    /// <summary> Kilit rölesinin output kanalı. </summary>
    public Guid LockIoChannelId { get; set; }

    /// <summary> Kilidi Açmak için output: <c>true(1)</c> || <c>false(0)</c> hangisi gönderilmeli. NO/NC kontrolü ayrıca çözülür bu kilidin kendi mantığıdır. </summary>
    public bool UnlockTurnsOn { get; set; } = true;

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
    public virtual SignalOuterDoor? OuterDoor { get; set; }
    public virtual SignalAuthority? Authority { get; set; }
    #endregion
}
