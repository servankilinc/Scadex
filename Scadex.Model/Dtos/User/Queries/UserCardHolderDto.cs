namespace Scadex.Model.Dtos.User.Queries;

/// <summary> Kart tanımlama penceresinin kullanıcı listesi (<c>ManageAccessCards</c>): yalnızca AKTİF kullanıcılar ve kartı atamak için gereken alanlar bulunur </summary>
public class UserCardHolderDto
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string FullName { get; set; } = null!;
    public string? IdentityCardId { get; set; }
}
