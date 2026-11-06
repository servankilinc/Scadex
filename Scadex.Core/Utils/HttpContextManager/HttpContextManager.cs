using Microsoft.AspNetCore.Http;
using Scadex.Core.Utils.ResultPattern;
using System.Security.Claims;

namespace Scadex.Core.Utils.HttpContextManager;

public class HttpContextManager : IHttpContextManager
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public HttpContextManager(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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