using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class ControlCard : IEntity
{
    public Guid DeviceId { get; set; }
    public string? MacAddress { get; set; }

    #region Erişim
    public string? Username { get; set; }
    /// <summary> Düz metin. Okuma DTO'larına yazılmaz; yalnızca <c>hasPassword</c> döner. </summary>
    public string? Password { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
