using Scadex.Core.Model;
using Microsoft.AspNetCore.Identity;

namespace Scadex.Model.Entities;

public class User : IdentityUser<Guid>, IEntity, IAuditableEntity, IActivatableEntity
{
    //public Guid Id { get; set; }
    //public string UserName { get; set; } = null!;
    //public string? Email { get; set; }
    //public string? PhoneNumber { get; set; }
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = null!;

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
    public virtual Company? Company { get; set; }
    public virtual ICollection<DeviceCommand>? DeviceCommands { get; set; }
    public virtual ICollection<RefreshToken>? RefreshTokens { get; set; }
    #endregion
}