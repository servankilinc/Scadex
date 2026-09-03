using CabinetOs.Core.Utils.Auth;
using CabinetOs.Core.Utils.HttpContextManager;
using CabinetOs.Core.Utils.ResultPattern;
using Microsoft.IdentityModel.Tokens;
using CabinetOs.Model.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CabinetOs.Business.Utils.TokenService;

public class TokenService : ITokenService
{
    private readonly TokenSettings _tokenSettings;
    private readonly IHttpContextManager _httpContextManager;
    public TokenService(TokenSettings tokenSettings, IHttpContextManager httpContextManager)
    {
        _tokenSettings = tokenSettings;
        _httpContextManager = httpContextManager;
    }


    public Result<AccessToken> GenerateAccessToken(IList<Claim> claims)
    {
        DateTime expiration = DateTime.UtcNow.AddMinutes(_tokenSettings.AccessTokenExpiration);
        SecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenSettings.SecurityKey));
        SigningCredentials signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);

        JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(
            issuer: _tokenSettings.Issuer,
            audience: _tokenSettings.Audience,
            claims: claims,
            expires: expiration,
            signingCredentials: signingCredentials
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
        if (string.IsNullOrWhiteSpace(token)) return Result<AccessToken>.Failure("Access token could not be created!");

        AccessToken accessToken = new AccessToken(token, expiration);
        return Result<AccessToken>.Success(accessToken);
    }

    public Result<RefreshToken> GenerateRefreshToken(User user, string tokenValue, string clientType, Guid? deviceId = default)
    {
        var ipAddress = _httpContextManager.GetClientIp();

        string tokenHash = HashToken(tokenValue);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            DeviceId = deviceId ?? Guid.NewGuid(),
            ClientType = clientType,
            IpAddress = ipAddress.IsSuccess ? ipAddress.Data : string.Empty,
            Token = tokenHash,
            ExpirationUtc = DateTime.UtcNow.AddMinutes(_tokenSettings.RefreshTokenExpiration),
            CreateDateUtc = DateTime.UtcNow,
            TTL = _tokenSettings.RefreshTokenTTL,
            IsRevoked = false
        };

        return Result<RefreshToken>.Success(refreshToken);
    }

    public string GenerateRandomNumber()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
