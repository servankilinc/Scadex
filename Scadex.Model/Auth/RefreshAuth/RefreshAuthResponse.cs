using Scadex.Core.Utils.Auth;

namespace Scadex.Model.Auth.Refresh;

public class RefreshAuthResponse
{
    public IList<string>? Roles { get; set; }
    public AccessToken AccessToken { get; set; } = null!;
}

public class RefreshAuthTrustedResponse : RefreshAuthResponse
{
    public string RefreshToken { get; set; } = null!;
}