using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Auth.Login;
using Scadex.Model.Auth.Logout;
using Scadex.Model.Auth.Refresh;
using Scadex.Model.Dtos.User.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;
using System.Security.Claims;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class AccountController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    public AccountController(IAuthService authService, IUserService userService, ILogger<AccountController> logger) : base(logger)
    {
        _authService = authService;
        _userService = userService;
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return ToAction(result);
    }


    [AllowAnonymous]
    [HttpPost("RefreshAuth")]
    public async Task<IActionResult> RefreshAuth(RefreshAuthRequest request)
    {
        var result = await _authService.RefreshAsync(request);

        return ToAction(result);
    }
    
    [Authorize]
    [HttpPost("Logout")]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        // Never trust a caller-supplied user id; bind it to the authenticated principal.
        if (!TryGetUserId(out var userId))
            return Forbid();
        request.UserId = userId;

        var result = await _authService.LogoutAsync(request);

        return ToAction(result);
    }

    [Authorize]
    [HttpPost("RevokeAll")]
    public async Task<IActionResult> RevokeAll()
    {
        if (!TryGetUserId(out var userId))
            return Forbid();

        var result = await _authService.RevokeAllAsync(userId);

        return ToAction(result);
    }

    #region Profile
    // Kendi hesabı üzerindeki işlemler: yalnızca giriş ister, izin İSTEMEZ ("veri yazan her uç izin ister" kuralının bilinçli
    // istisnası — yazılan tek kayıt çağıranın kendisidir). Kimlik her zaman token'dan okunur; başkasının hesabı `UserController`'dadır.

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();

        var result = await _userService.GetProfileAsync(userId, cancellationToken);

        return ToAction(result);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UserProfileUpdateDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();

        var result = await _userService.UpdateProfileAsync(userId, request, cancellationToken);

        return ToAction(result);
    }

    /// <summary> Mevcut parolayı sorarak değiştirir; gönderen cihaz dışındaki oturumlar yeniden giriş ister. </summary>
    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(UserOwnPasswordChangeDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();

        var result = await _userService.ChangeOwnPasswordAsync(userId, request, cancellationToken);

        return ToAction(result);
    }
    #endregion

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}