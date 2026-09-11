using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// SANAL ic kapi: bir anahtar (switch, <c>Input</c>) kanali ile bir kilit (<c>Output</c>) kanalinin ad ve kurumla eslesmesi.
/// Kurum yetkilendirme zinciri: <c>User → Rol → SignalAuthority → SignalInnerDoor → LockIoChannelId</c>.
/// Kabinde her kurumun EN FAZLA bir aktif ic kapisi olur; kart okuma bu sayede tek kapiya cozulur.
/// </summary>
public class SignalInnerDoor : IEntity, IAuditableEntity, IActivatableEntity
{
    /// <summary> Istemci uretir. </summary>
    public Guid Id { get; set; }
    public Guid OuterDoorId { get; set; }
    public string Name { get; set; } = null!;
    public Guid AuthorityId { get; set; }

    /// <summary> Ic kapi anahtarinin giris kanali — FK DEGIL. Kapinin gercekten acilip kapandigini bu dogrular. </summary>
    public Guid SwitchIoChannelId { get; set; }
    public string SwitchOpenValue { get; set; } = "1";

    /// <summary> Kilit rolesinin cikis kanali — FK DEGIL. Komut aninda cihaz bu kanaldan turetilir (role karti). </summary>
    public Guid LockIoChannelId { get; set; }

    /// <summary>
    /// Kilidi ACMAK icin role enerjilenir mi? Fail-secure kilitte <c>true</c>, fail-safe (manyetik) kilitte <c>false</c>.
    /// NO/NC terslemesi ayrica cekirdekte kablolamadan cozulur; bu alan kilidin kendi mantigidir.
    /// </summary>
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
    public virtual SignalInnerDoorState? State { get; set; }
    #endregion
}
