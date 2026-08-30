using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Scadex.Core.Enums;
using Scadex.Core.Utils.Localization;
using Scadex.Core.Utils.ResultPattern;
using System.Security.Claims;

namespace Scadex.Core.Utils.HttpContextManager;

public class HttpContextManager : IHttpContextManager
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LocalizationSettings _localizationSettings;
    public HttpContextManager(IHttpContextAccessor httpContextAccessor, LocalizationSettings localizationSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationSettings = localizationSettings;
    }


    public Result<string> GetNameIdentifier()
    {
        if (_httpContextAccessor.HttpContext == null)
            return Result<string>.Failure("Not exist HttpContext inside HttpContextManager.GetNameIdentifier!");

        var id = _httpContextAccessor.HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(id) ? Result<string>.NotFound() : Result<string>.Success(id);
    }

    public Result<string> GetName()
    {
        if (_httpContextAccessor.HttpContext == null)
            return Result<string>.Failure("Not exist HttpContext inside HttpContextManager.GetName!");

        var name = _httpContextAccessor.HttpContext.User?.FindFirst(ClaimTypes.Name)?.Value;
        return string.IsNullOrWhiteSpace(name) ? Result<string>.NotFound() : Result<string>.Success(name);
    }

    public Result<string> GetUserAgent()
    {
        if (_httpContextAccessor.HttpContext == null)
            return Result<string>.Failure("Not exist HttpContext inside HttpContextManager.GetUserAgent!");

        var userAggent = _httpContextAccessor.HttpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAggent) ? Result<string>.NotFound() : Result<string>.Success(userAggent);
    }

    public Result<string> GetClientIp()
    {
        if (_httpContextAccessor.HttpContext == null)
            return Result<string>.Failure("Not exist HttpContext inside HttpContextManager.GetClientIp!");

        var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ipAddress) ? Result<string>.NotFound() : Result<string>.Success(ipAddress);
    }


    #region Culture & Language
    public Result<string> GetCurrentCulture()
    {
        string defaultCulture = _localizationSettings.DefaultLanguage.GetDescription();
        if (_httpContextAccessor.HttpContext == null)
            return Result<string>.Success(defaultCulture);

        var cookieName = CookieRequestCultureProvider.DefaultCookieName;
        var cookieValue = _httpContextAccessor.HttpContext.Request.Cookies[cookieName];
        if (string.IsNullOrWhiteSpace(cookieValue))
            return Result<string>.Success(defaultCulture);

        var requestCulture = CookieRequestCultureProvider.ParseCookieValue(cookieValue);
        var cultureInfo = requestCulture?.Cultures.FirstOrDefault().Value;
        if (string.IsNullOrWhiteSpace(cultureInfo))
            return Result<string>.Success(defaultCulture);

        return Result<string>.Success(cultureInfo);
    }

    public Result<Language> GetCurrentLanguage()
    {
        Result<string> cultureInfo = GetCurrentCulture();
        if (!cultureInfo.IsSuccess)
            return Result<Language>.Failure(cultureInfo.Message);

        Language language = GlobalExtensions.GetEnumByDescription<Language>(cultureInfo.Data);
        return Result<Language>.Success(language);
    }

    public Result<byte> GetCurrentLanguageId()
    {
        Result<string> cultureInfo = GetCurrentCulture();
        if (!cultureInfo.IsSuccess)
            return Result<byte>.Failure(cultureInfo.Message);

        return Result<byte>.Success((byte)GlobalExtensions.GetEnumByDescription<Language>(cultureInfo.Data));
    }

    public Result SetCurrentCulture(string culture)
    {
        if (_httpContextAccessor.HttpContext == null)
            return Result.Failure("Not exist HttpContext inside HttpContextManager.SetCurrentCulture!");

        var cookieName = CookieRequestCultureProvider.DefaultCookieName;
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));

        _httpContextAccessor.HttpContext.Response.Cookies.Append(cookieName, cookieValue, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddMonths(1),
            IsEssential = true,
            Secure = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
        });

        return Result.Success();
    }
    #endregion

    #region Refresh Token

    /// <summary> 
    /// Cookie Path'i, cookie'yi okuyan TUM uclari kapsamalidir: RefreshAuth ve Logout. 
    /// Tarayici Path eslesmesini buyuk/kucuk harfe duyarli yapar; istemci de uclari bu controller yolu ile cagirmalidir, aksi halde cookie hic gonderilmez.
    /// </summary>
    private const string RefreshTokenCookiePath = "/api/Account";
    private const string RefreshTokenCookieName = "Auth_RefreshToken";
    private static CookieOptions BuildRefreshTokenCookieOptions(DateTime? expirationUtc = null) => new CookieOptions
    {
        Secure = true,
        HttpOnly = true,
        Expires = expirationUtc,
        SameSite = SameSiteMode.Lax,
        Path = RefreshTokenCookiePath
    };


    public Result<string> GetRefreshTokenFromCookie()
    {
        if (_httpContextAccessor.HttpContext == null) return Result<string>.Failure("Not exist HttpContext inside HttpContextManager.GetRefreshTokenFromCookie!");

        string? refreshToken = _httpContextAccessor.HttpContext.Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result<string>.Failure("Not exist refresh token inside cookie!");

        return Result<string>.Success(refreshToken);
    }

    public Result AddRefreshTokenToCookie(string refreshToken, DateTime expirationUtc)
    {
        if (_httpContextAccessor.HttpContext == null) return Result.Failure("Not exist HttpContext inside HttpContextManager.AddRefreshTokenToCookie!");

        _httpContextAccessor.HttpContext.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildRefreshTokenCookieOptions(expirationUtc));

        return Result.Success();
    }

    public Result DeletetRefreshTokenFromCookie()
    {
        if (_httpContextAccessor.HttpContext == null) return Result.Failure("Not exist HttpContext inside HttpContextManager.DeletetRefreshTokenFromCookie!");

        // Path/Secure/SameSite verilmezse tarayici mevcut cookie ile eslestiremez ve cookie hayatta kalir; cikis yapan oturum silinmemis olur.
        _httpContextAccessor.HttpContext.Response.Cookies.Delete(RefreshTokenCookieName, BuildRefreshTokenCookieOptions());

        return Result.Success();
    }
    #endregion
}