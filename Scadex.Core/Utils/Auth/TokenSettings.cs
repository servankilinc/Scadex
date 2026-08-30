namespace Scadex.Core.Utils.Auth;

public class TokenSettings
{
    public string Audience { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string SecurityKey { get; set; } = null!;
    public int AccessTokenExpiration { get; set; }
    public int RefreshTokenExpiration { get; set; }
    public int RefreshTokenTTL { get; set; }
}
