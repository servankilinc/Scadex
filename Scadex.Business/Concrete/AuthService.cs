using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.TokenService;
using Scadex.Core.Enums;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Auth;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Auth.Login;
using Scadex.Model.Auth.Logout;
using Scadex.Model.Auth.Refresh;
using Scadex.Model.Dtos.User.Queries;
using Scadex.Model.Entities;
using System.Security.Claims;

namespace Scadex.Business.Concrete;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IHttpContextManager _httpContextManager;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public AuthService(IUnitOfWork unitOfWork, ITokenService tokenService, UserManager<User> userManager, SignInManager<User> signInManager, IHttpContextManager httpContextManager, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextManager = httpContextManager;
        _validationService = validationService;
        _mapper = mapper;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(loginRequest, cancellationToken);
        if (!validationResult.IsValid)
            return Result<LoginResponse>.Validation(validationResult.Failures);

        // 1) Find user by credentials
        User? user = null;
        if (loginRequest.Email != null)
        {
            user = await _userManager.FindByEmailAsync(loginRequest.Email);
        }
        else if (loginRequest.UserName != null)
        {
            user = await _userManager.FindByNameAsync(loginRequest.UserName);
        }

        // 2) Check CanSignInAsync and CheckPasswordSignInAsync (Rejected credentials are a client-side condition (403), not a server fault (500))
        if (user == null)
            return Result<LoginResponse>.Forbidden(message: "Credentials are incorrect.", metadata: GlobalExtensions.Meta("Requester Email or Username", loginRequest.Email ?? loginRequest.UserName));
        SignInResult checkPassword = await _signInManager.CheckPasswordSignInAsync(user, loginRequest.Password, lockoutOnFailure: true);
        if (!checkPassword.Succeeded)
        {
            if (checkPassword.IsLockedOut)
                return Result<LoginResponse>.Forbidden(message: "Your account is temporarily locked due to multiple failed login attempts.", metadata: GlobalExtensions.Meta("Requester Email", loginRequest.Email));
            if (checkPassword.RequiresTwoFactor)
                return Result<LoginResponse>.Forbidden(message: "Two-factor authentication is required to login.", metadata: GlobalExtensions.Meta("Requester Email", loginRequest.Email));
            if (checkPassword.IsNotAllowed)
                return Result<LoginResponse>.Forbidden(message: "The user is not allowed to sign in.", metadata: GlobalExtensions.Meta("Requester Email", loginRequest.Email));
            return Result<LoginResponse>.Forbidden(message: "Credentials are incorrect.", metadata: GlobalExtensions.Meta("Requester Email or Username", loginRequest.Email ?? loginRequest.UserName));
        }

        if (!await _signInManager.CanSignInAsync(user))
        {
            return Result<LoginResponse>.Forbidden(message: "You are not allowed to login.", metadata: GlobalExtensions.Meta("User", user));
        }

        // kontrol edilmezse "kullaniciyi pasife al" islemi hicbir sey yapmamis olur.
        if (!user.IsActive)
        {
            return Result<LoginResponse>.Forbidden(message: "Hesabiniz pasif durumda. Lutfen yoneticinizle iletisime gecin.", metadata: GlobalExtensions.Meta("Inactive User", user.UserName));
        }

        // 3) Get user roles and claims
        IList<string> roles = await _userManager.GetRolesAsync(user);
        IList<Claim> claims = await GetClaimsAsync(user, roles);

        // 4) Generate Access Token and Refresh Token
        Result<AccessToken> accessToken = _tokenService.GenerateAccessToken(claims);
        if (!accessToken.IsSuccess)
            return Result<LoginResponse>.Failure(description: "Access token could not generated", metadata: GlobalExtensions.Meta("Access Token Result", accessToken));
        string tokenValue = _tokenService.GenerateRandomNumber();
        Result<RefreshToken> refreshToken = _tokenService.GenerateRefreshToken(user, tokenValue, loginRequest.ClientType, loginRequest.DeviceId);
        if (!refreshToken.IsSuccess)
            return Result<LoginResponse>.Failure(description: "Refresh token could not generated", metadata: GlobalExtensions.Meta("Refresh Token Result", refreshToken));

        // 5) Save Refresh Token and Revoke old ones if deviceId is provided
        if (loginRequest.DeviceId != null && loginRequest.DeviceId.HasValue)
        {
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.DeviceId == loginRequest.DeviceId.Value && f.IsRevoked == false);
        }

        await _unitOfWork.RefreshTokens.AddAndSaveAsync(refreshToken.Data, cancellationToken);
        if (refreshToken.Data.ClientType != ClientType.Web)
        {
            return Result<LoginResponse>.Success(new LoginTrustedResponse
            {
                AccessToken = accessToken.Data,
                RefreshToken = tokenValue,
                DeviceId = refreshToken.Data.DeviceId,
                User = _mapper.Map<UserBaseDto>(user),
                Roles = roles,
                Permissions = await GetPermissionCodesAsync(roles, cancellationToken)
            });
        }
        else
        {
            _httpContextManager.AddRefreshTokenToCookie(tokenValue, refreshToken.Data.ExpirationUtc);
            return Result<LoginResponse>.Success(new LoginResponse
            {
                AccessToken = accessToken.Data,
                DeviceId = refreshToken.Data.DeviceId,
                User = _mapper.Map<UserBaseDto>(user),
                Roles = roles,
                Permissions = await GetPermissionCodesAsync(roles, cancellationToken)
            });
        }
    }

    public async Task<Result<RefreshAuthResponse>> RefreshAsync(RefreshAuthRequest refreshAuthRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _validationService.ValidateAsync(refreshAuthRequest, cancellationToken);
            if (!validationResult.IsValid)
                return Result<RefreshAuthResponse>.Validation(validationResult.Failures);
            // 1) Set refresh token from cookie if not provided
            if (string.IsNullOrWhiteSpace(refreshAuthRequest.RefreshToken))
            {
                var cookieValue = _httpContextManager.GetRefreshTokenFromCookie();
                if (!cookieValue.IsSuccess)
                    return Result<RefreshAuthResponse>.Forbidden(message: "Your session has expired. Please sign in again.", description: "Refresh auth request cookie not found in cookie", metadata: GlobalExtensions.Meta("Cookie Result", cookieValue.Error.Description));
                refreshAuthRequest.RefreshToken = cookieValue.Data;
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            string hashedToken = _tokenService.HashToken(refreshAuthRequest.RefreshToken);
            // 2) Find refresh token record
            RefreshToken? refreshToken = await _unitOfWork.RefreshTokens.GetAsync(where: f => f.UserId == refreshAuthRequest.UserId && f.DeviceId == refreshAuthRequest.DeviceId && f.Token == hashedToken && f.TTL > 0 && f.IsRevoked == false && f.ExpirationUtc > DateTime.UtcNow, cancellationToken: cancellationToken);
            if (refreshToken == null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<RefreshAuthResponse>.Forbidden(message: "Your session has expired. Please sign in again.", description: "There is no refresh token that can be used.", metadata: GlobalExtensions.Meta("Request Model", refreshAuthRequest));
            }

            // 3) Find user
            var user = await _unitOfWork.Users.GetAsync(where: f => f.Id == refreshAuthRequest.UserId, cancellationToken: cancellationToken);
            // Pasif kullanici Login'de oldugu gibi burada da reddedilir; aksi halde elindeki refresh token ile 7 gun daha oturum yenilerdi.
            if (user == null || !user.IsActive)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<RefreshAuthResponse>.Forbidden(message: "Your session has expired. Please sign in again.", description: $"User cannot found for refresh auth, userId: {refreshAuthRequest.UserId}", metadata: GlobalExtensions.Meta("Request Model", refreshAuthRequest));
            }

            // 4) Update refresh token 
            string tokenValue = _tokenService.GenerateRandomNumber();
            refreshToken.Token = _tokenService.HashToken(tokenValue);
            refreshToken.TTL -= 1;
            if (refreshToken.TTL <= 0)
                refreshToken.IsRevoked = true;
            await _unitOfWork.RefreshTokens.UpdateAndSaveAsync(refreshToken, cancellationToken);
            // 5) revoke old tokens for the device
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.DeviceId == refreshAuthRequest.DeviceId && f.IsRevoked == false && f.Id != refreshToken.Id, cancellationToken);
            // 6) Get user roles and claims
            IList<string> roles = await _userManager.GetRolesAsync(user);
            IList<Claim> claims = await GetClaimsAsync(user, roles);
            // 7) Generate new access token
            Result<AccessToken> accessToken = _tokenService.GenerateAccessToken(claims);
            if (!accessToken.IsSuccess)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<RefreshAuthResponse>.Failure(description: "Access token could not generated", metadata: GlobalExtensions.Meta("Access Token Result", accessToken));
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            if (refreshToken.ClientType != ClientType.Web)
            {
                return Result<RefreshAuthResponse>.Success(new RefreshAuthTrustedResponse { AccessToken = accessToken.Data, RefreshToken = tokenValue, Roles = roles });
            }
            else
            {
                _httpContextManager.AddRefreshTokenToCookie(tokenValue, refreshToken.ExpirationUtc);
                return Result<RefreshAuthResponse>.Success(new RefreshAuthResponse { AccessToken = accessToken.Data, Roles = roles });
            }
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> LogoutAsync(LogoutRequest logoutRequest, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(logoutRequest, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);
        // Fall back to the cookie when the caller did not send the token explicitly (web clients).
        if (string.IsNullOrWhiteSpace(logoutRequest.RefreshToken))
        {
            var cookieValue = _httpContextManager.GetRefreshTokenFromCookie();
            if (cookieValue.IsSuccess)
                logoutRequest.RefreshToken = cookieValue.Data;
        }

        if (!string.IsNullOrWhiteSpace(logoutRequest.RefreshToken))
        {
            string hashedToken = _tokenService.HashToken(logoutRequest.RefreshToken);
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == logoutRequest.UserId && f.DeviceId == logoutRequest.DeviceId && f.Token == hashedToken && f.IsRevoked == false, cancellationToken);
        }
        else
        {
            // No token supplied: revoke everything issued to this device.
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == logoutRequest.UserId && f.DeviceId == logoutRequest.DeviceId && f.IsRevoked == false, cancellationToken);
        }

        _httpContextManager.DeletetRefreshTokenFromCookie();
        return Result.Success();
    }

    public async Task<Result> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == default)
            return Result.Validation(new Dictionary<string, string[]> { ["userId"] = new[] { "A valid user id is required." } }, message: "A valid user id is required.");
        await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == userId && f.IsRevoked == false, cancellationToken);
        // Rotating the security stamp invalidates cookies and any stamp-bound tokens.
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
            await _userManager.UpdateSecurityStampAsync(user);
        _httpContextManager.DeletetRefreshTokenFromCookie();
        return Result.Success();
    }

    private async Task<ICollection<string>> GetPermissionCodesAsync(IList<string> roleNames, CancellationToken cancellationToken = default)
    {
        if (roleNames.Count == 0)
            return Array.Empty<string>();

        var normalizedNames = roleNames.Select(r => r.ToUpperInvariant()).ToList();
        var roleIds = await _unitOfWork.Roles.GetAllAsync<Guid>(
            select: r => r.Id,
            // Pasif rol izin turetmez. Rol claim'i (GetRolesAsync) yine yazilir; pasiflik yalnizca izin kumesini etkiler.
            where: r => r.NormalizedName != null && normalizedNames.Contains(r.NormalizedName) && r.IsActive,
            cancellationToken: cancellationToken);

        if (roleIds == null || roleIds.Count == 0)
            return Array.Empty<string>();

        var codes = await _unitOfWork.RolePermissions.GetAllAsync<string>(
            select: rp => rp.Permission!.Code,
            where: rp => roleIds.Contains(rp.RoleId),
            cancellationToken: cancellationToken);

        // Birden fazla rol ayni izni tasiyabilir; token'a mukerrer claim yazmamak icin tekillestirilir.
        return codes == null ? Array.Empty<string>() : codes.Distinct().ToArray();
    }

    private async Task<IList<Claim>> GetClaimsAsync(User user, IList<string>? roles = default)
    {
        string displayName = user.FullName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = user.UserName ?? user.Email ?? user.Id.ToString();
        List<Claim> claimList = new List<Claim>()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, displayName),
            // veritabanina gitmeden kullanicinin sirketi bilinsin.
            new Claim(AppClaimTypes.CompanyId, user.CompanyId.ToString())
        };
        if (!string.IsNullOrEmpty(user.Email))
            claimList.Add(new Claim(ClaimTypes.Email, user.Email));
        IList<Claim>? persistentClaims = await _userManager.GetClaimsAsync(user);
        claimList.AddRange(persistentClaims);
        roles ??= await _userManager.GetRolesAsync(user);
        claimList.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Rol -> izin eslemesi token'a gomulur; yetkilendirme her istekte DB'ye gitmez.
        ICollection<string> permissions = await GetPermissionCodesAsync(roles);
        claimList.AddRange(permissions.Select(code => new Claim(AppClaimTypes.Permission, code)));

        return claimList;
    }
}
