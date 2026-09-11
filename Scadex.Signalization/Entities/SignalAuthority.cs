using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Ic kapi yetkisi taşıyan kurum (Belediye, Emniyet, Sinyalizasyon...). Kurum kod degil VERIDIR ve cekirdekteki bir
/// <b>role</b> baglanir: personelin kurumu = sahip oldugu kurum rolu. Boylece ikinci bir yetki yapisi dogmaz;
/// kullanici/rol altyapisi tektir. Bir personelin EN FAZLA bir kurum rolu olur (kabin basina tek ic kapi).
/// </summary>
public class SignalAuthority : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary> Cekirdekteki <c>Role.Id</c> — FK DEGIL (farkli context). Aktif kurumlar arasinda tekildir. </summary>
    public Guid RoleId { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion

    #region --- IActivatableEntity ---
    public bool IsActive { get; set; }
    #endregion
}
