using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary> 
/// İç kapılarda yetkili kurum bilgisini temsil eder. Her bir <see cref="SignalInnerDoor"/> bu entitye bağlı 1-1 ilişkilidir.
/// <para/>
/// Sinyalizasyon modülündeki yetkili kurum tanımı(SignalAuthority) Scadex çekirdeğindeki bir <b>role</b> ile ilişkilendirilir ve
/// Sinyalizasyon modülündeki operatörler Scadex çekirdeğindeki User tablosunda tutulur bu sayede bir Operatör(User) Role ilişkisi tanımladığında 
/// ilgili role sahip yetkili kurum(SignalAuthority)'da çalıştığı anlaşılır. Böylece ikinci bir yetki yapısına gerek kalmaz.
/// </summary>
public class SignalAuthority : IEntity, IAuditableEntity, IActivatableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary> Scadex çekirdeğindeki <c>Role.Id</c>. </summary>
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
