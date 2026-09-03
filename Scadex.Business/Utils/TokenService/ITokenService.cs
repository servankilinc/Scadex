using Scadex.Core.Utils.Auth;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;
using System.Security.Claims;

namespace Scadex.Business.Utils.TokenService;

public interface ITokenService
{
    Result<AccessToken> GenerateAccessToken(IList<Claim> claims);
    Result<RefreshToken> GenerateRefreshToken(User user, string tokenValue, string clientType, Guid? deviceId = default);
    string GenerateRandomNumber();
    string HashToken(string token);
}