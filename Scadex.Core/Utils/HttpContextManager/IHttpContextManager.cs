using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Core.Utils.HttpContextManager;

public interface IHttpContextManager
{
    Result<string> GetNameIdentifier();
    Result<string> GetName();
    Result<string> GetUserAgent();
    Result<string> GetClientIp();

    #region Refresh Token
    Result<string> GetRefreshTokenFromCookie();
    Result AddRefreshTokenToCookie(string refreshToken, DateTime expirationUtc);
    Result DeletetRefreshTokenFromCookie();
    #endregion
}