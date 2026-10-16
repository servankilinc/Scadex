using Scadex.Core.Model;

namespace Scadex.Model.Dtos.User.Queries;

/// <summary>
/// Giriş yapan kullanıcının kendi profili (<c>GET /api/Account/profile</c>). Yalnızca kimlikten okunur, izin istemez.
/// Kullanıcı adı, firma ve kart burada salt okunurdur: onları yönetici (<c>ManageUsers</c> / <c>ManageAccessCards</c>) yazar.
/// </summary>
public class UserProfileDto : IDto
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? IdentityCardId { get; set; }
}
