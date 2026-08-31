using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class RefreshToken : IEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public string? ClientType { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpirationUtc { get; set; }
    public DateTime CreateDateUtc { get; set; }
    public int TTL { get; set; }
    public bool IsRevoked { get; set; }
    
    #region *** EF Core Navigation ***
    public virtual User? User { get; set; } 
    #endregion
}