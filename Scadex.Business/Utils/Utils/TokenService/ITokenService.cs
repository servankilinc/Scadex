using System.Security.Claims;
using CabinetOs.Core.Utils.Auth;
using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Entities;

namespace CabinetOs.Business.Utils.TokenService;

public interface ITokenService
{
    Result<AccessToken> GenerateAccessToken(IList<Claim> claims);
    Result<RefreshToken> GenerateRefreshToken(User user, string tokenValue, string clientType, Guid? deviceId = default);
    string GenerateRandomNumber();
    string HashToken(string token);
}