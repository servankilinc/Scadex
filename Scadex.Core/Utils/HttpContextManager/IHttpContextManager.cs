using Scadex.Core.Enums;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Core.Utils.HttpContextManager;

public interface IHttpContextManager
{
    Result<string> GetNameIdentifier();
    Result<string> GetName();
    Result<string> GetUserAgent();
    Result<string> GetClientIp();

    #region Culture Language
    Result<string> GetCurrentCulture();
    Result<byte> GetCurrentLanguageId();
    Result<Language> GetCurrentLanguage();
    Result SetCurrentCulture(string culture);
    #endregion

    #region Refresh Token
    Result<string> GetRefreshTokenFromCookie();
    Result AddRefreshTokenToCookie(string refreshToken, DateTime expirationUtc);
    Result DeletetRefreshTokenFromCookie();
    #endregion
}