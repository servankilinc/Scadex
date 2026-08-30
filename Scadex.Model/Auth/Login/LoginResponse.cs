using Scadex.Core.Utils.Auth;
using Scadex.Model.Dtos.User.Queries;

namespace Scadex.Model.Auth.Login;

public class LoginResponse
{
    public IList<string>? Roles { get; set; }
    public ICollection<string> Permissions { get; set; } = new List<string>();

    public AccessToken AccessToken { get; set; } = null!;
    public Guid DeviceId { get; set; }
    public UserBaseDto User { get; set; } = null!;
}

public class LoginTrustedResponse : LoginResponse
{
    public string RefreshToken { get; set; } = null!;
}
