using Scadex.Core.Model;

namespace Scadex.Model.Dtos.UserRole.Queries;

/// <summary> Bir role atanmış kullanıcı (<c>GET /api/UserRole/role/{roleId}/users</c>). Pasif kullanıcılar da listelenir. </summary>
public class RoleMemberDto : IDto
{
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
}
