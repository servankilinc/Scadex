using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Model.Dtos.User.Queries;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly UserManager<User> _userManager;
    private readonly IMapper _mapper;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ILogger<UserService> _logger;
    public UserService(IUnitOfWork unitOfWork, IValidationService validationService, UserManager<User> userManager, IMapper mapper, IHttpContextManager httpContextManager, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _userManager = userManager;
        _mapper = mapper;
        _httpContextManager = httpContextManager;
        _logger = logger;
    }

    #region List
    public async Task<Result<ICollection<UserDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAllAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<UserDetailDto>>.NotFound();

        // User'da UserRoles navigasyonu yok (Identity ara tablosu); roller ayrı tek sorguyla eklenir.
        var rolesByUser = await _unitOfWork.Users.GetRoleNamesByUserAsync(cancellationToken);
        foreach (var user in result)
            user.Roles = rolesByUser.GetValueOrDefault(user.Id) ?? new List<string>();

        return Result<ICollection<UserDetailDto>>.Success(result);
    }

    public async Task<Result<ICollection<UserCardHolderDto>>> GetCardHoldersAsync(CancellationToken cancellationToken = default)
    {
        // Pasif kullanıcıya kart atanamaz (AssignIdentityCardAsync 404 döner); listede de yer almaz.
        var users = await _unitOfWork.Users.GetAllAsync(
            select: u => new UserCardHolderDto { Id = u.Id, UserName = u.UserName, FullName = u.FullName, IdentityCardId = u.IdentityCardId },
            where: u => u.IsActive,
            cancellationToken: cancellationToken) ?? [];

        return Result<ICollection<UserCardHolderDto>>.Success(users.OrderBy(u => u.FullName).ToList());
    }
    #endregion

    #region Create
    public async Task<Result> CreateAsync(UserCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: $"Validation failed for UserCreateDto");

        var companyExists = await _unitOfWork.Companies.IsExistAsync(where: f => f.Id == request.CompanyId, cancellationToken: cancellationToken);
        if (!companyExists)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.CompanyId)] = new[] { "Belirtilen firma bulunamadi." } }, message: "Belirtilen firma bulunamadi.");

        if (await _userManager.FindByNameAsync(request.UserName) != null)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.UserName)] = new[] { "Bu kullanici adi zaten kullaniliyor." } }, message: "Bu kullanici adi zaten kullaniliyor.");

        // E-posta isteğe bağlı: boş metin null'a çekilir. Doluysa benzersiz olmalı — Identity bunu artık denetlemiyor
        // (`RequireUniqueEmail = false`, Program.cs) ve e-postayla giriş tekil eşleşmeye dayanır.
        request.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.Email != null && await _userManager.FindByEmailAsync(request.Email) != null)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.Email)] = new[] { "Bu e-posta adresi zaten kullaniliyor." } }, message: "Bu e-posta adresi zaten kullaniliyor.");

        // Kullanıcı kartı normalize edilir ve benzersiz olmalı
        request.IdentityCardId = NormalizeIdentityCardId(request.IdentityCardId);
        var cardConflict = await ValidateIdentityCardIdAsync(request.IdentityCardId, excludeUserId: null, cancellationToken);
        if (cardConflict != null)
            return cardConflict;

        var user = _mapper.Map<User>(request);
        user.IsActive = true;
        var identityResult = await _userManager.CreateAsync(user, request.Password);
        if (!identityResult.Succeeded)
            return Result.Failure(description: "User cannot be created.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return Result.Success();
    }
    #endregion

    #region Update
    public async Task<Result> UpdateAsync(UserUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);

        // Login IsActive'e baktigi icin kendini pasife alan yonetici sistemden kilitlenirdi.
        var currentUserId = _httpContextManager.GetNameIdentifier();
        if (!request.IsActive && currentUserId.IsSuccess && Guid.TryParse(currentUserId.Data, out var selfId) && selfId == request.Id)
        {
            const string message = "Kendi hesabınızı pasife alamazsınız.";
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.IsActive)] = new[] { message } }, message: message);
        }

        var entity = await _unitOfWork.Users.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();

        // Pasife alinan kullanicinin karti index filtresi disinda kalir; cakisma yalnizca aktif kalacak kullanicida aranir.
        request.IdentityCardId = NormalizeIdentityCardId(request.IdentityCardId);
        if (request.IsActive)
        {
            var cardConflict = await ValidateIdentityCardIdAsync(request.IdentityCardId, excludeUserId: request.Id, cancellationToken);
            if (cardConflict != null)
                return cardConflict;
        }

        bool isDeactivated = entity.IsActive && !request.IsActive;
        await _unitOfWork.Users.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);

        // Access token (24 sa) suresi dolana kadar gecerli kalir — istek basina IsActive kontrolu bilerek yok.
        if (isDeactivated)
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == request.Id && f.IsRevoked == false, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Kullanıcıya yalnızca kart numarası yazar (sahada okutulan kartı tanıtma). Kart başka bir aktif kullanıcıdaysa 400;
    /// kullanıcının FARKLI bir kartı varsa <see cref="UserIdentityCardAssignDto.ReplaceExisting"/> olmadan 400 — eski kart sessizce ezilmesin.
    /// </summary>
    public async Task<Result> AssignIdentityCardAsync(Guid userId, UserIdentityCardAssignDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);

        var identityCardId = NormalizeIdentityCardId(request.IdentityCardId);
        if (identityCardId == null)
        {
            const string message = "Kart numarası boş olamaz.";
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.IdentityCardId)] = new[] { message } }, message: message);
        }

        // Pasif kullanıcının kartı index filtresi dışında kalır ve kart okumasında eşleşmez; atamak anlamsızdır.
        var entity = await _unitOfWork.Users.GetAsync(where: (f) => f.Id == userId && f.IsActive, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound(message: "Kullanıcı bulunamadı veya pasif durumda.");

        if (entity.IdentityCardId == identityCardId)
            return Result.Success();

        var cardConflict = await ValidateIdentityCardIdAsync(identityCardId, excludeUserId: userId, cancellationToken);
        if (cardConflict != null)
            return cardConflict;

        if (entity.IdentityCardId != null && !request.ReplaceExisting)
        {
            string message = $"{entity.FullName} kullanıcısının zaten tanımlı bir kartı var ({entity.IdentityCardId}).";
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.ReplaceExisting)] = new[] { message } }, message: message);
        }

        var previousCardId = entity.IdentityCardId;
        entity.IdentityCardId = identityCardId;
        await _unitOfWork.Users.UpdateAndSaveAsync(entity, cancellationToken);

        _logger.LogInformation("Kart tanimlama: kullanici {UserId} karti {PreviousCardId} -> {CardId}", userId, previousCardId ?? "(yok)", identityCardId);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, UserPasswordChangeDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result.NotFound(message: "Kullanıcı bulunamadı.");

        // Sıfırlama token'ı ile: eski parola sorulmaz ve güvenlik damgası (SecurityStamp) yenilenir.
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var identityResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!identityResult.Succeeded)
        {
            // Parola kuralı ihlali istemci hatasıdır (400), sunucu hatası değil.
            var messages = identityResult.Errors.Select(e => e.Description).ToArray();
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.NewPassword)] = messages }, message: messages.FirstOrDefault());
        }

        // Yanlış denemelerle kilitlenmiş kullanıcı yeni parolayla hemen girebilsin.
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        // Açık oturumlar yeni parolayla yeniden girmeli; access token (24 sa) süresi dolana kadar geçerli kalır.
        await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == userId && f.IsRevoked == false, cancellationToken);

        _logger.LogInformation("Parola degistirildi: kullanici {UserId}", userId);
        return Result.Success();
    }
    #endregion

    #region Profile
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Pasif kullanıcının access token'ı süresi dolana kadar geçerli kalır; profil ona açılmaz.
        var profile = await _unitOfWork.Users.GetAsync<UserProfileDto>(configurationProvider: _mapper.ConfigurationProvider, where: u => u.Id == userId && u.IsActive, cancellationToken: cancellationToken);
        if (profile == null)
            return Result<UserProfileDto>.NotFound(message: "Kullanıcı bulunamadı veya pasif durumda.");
        return Result<UserProfileDto>.Success(profile);
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(Guid userId, UserProfileUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<UserProfileDto>.Validation(validationResult.Failures);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive)
            return Result<UserProfileDto>.NotFound(message: "Kullanıcı bulunamadı veya pasif durumda.");

        // E-posta benzersizliği Identity'de kapalı (`RequireUniqueEmail = false`); CreateAsync'teki denetimin aynısı.
        // İkinci bir eşleşme FindByEmailAsync'i (giriş) istisnaya düşürürdü.
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email != null)
        {
            var owner = await _userManager.FindByEmailAsync(email);
            if (owner != null && owner.Id != userId)
            {
                const string message = "Bu e-posta adresi zaten kullaniliyor.";
                return Result<UserProfileDto>.Validation(new Dictionary<string, string[]> { [nameof(request.Email)] = new[] { message } }, message: message);
            }
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        // E-posta repository ile yazılmaz: `NormalizedEmail` güncellenmez ve e-postayla giriş eski adresi arar.
        // SetEmailAsync diğer alanlarla birlikte tek UpdateAsync'te kaydeder.
        var identityResult = string.Equals(email, user.Email, StringComparison.Ordinal)
            ? await _userManager.UpdateAsync(user)
            : await _userManager.SetEmailAsync(user, email);
        if (!identityResult.Succeeded)
            return Result<UserProfileDto>.Failure(description: "User profile cannot be updated.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<Result> ChangeOwnPasswordAsync(Guid userId, UserOwnPasswordChangeDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive)
            return Result.NotFound(message: "Kullanıcı bulunamadı veya pasif durumda.");

        // ChangePasswordAsync mevcut parolayı doğrular ve güvenlik damgasını (SecurityStamp) yeniler.
        var identityResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!identityResult.Succeeded)
        {
            // Yanlış mevcut parola da parola kuralı ihlali de istemci hatasıdır (400); hata ilgili alana yazılır.
            var mismatch = identityResult.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            var messages = mismatch ? new[] { "Mevcut parola hatalı." } : identityResult.Errors.Select(e => e.Description).ToArray();
            var field = mismatch ? nameof(request.CurrentPassword) : nameof(request.NewPassword);
            return Result.Validation(new Dictionary<string, string[]> { [field] = messages }, message: messages.FirstOrDefault());
        }

        // Diğer cihazlar yeni parolayla yeniden girmeli; bu cihazın oturumu korunur (kullanıcı kendini dışarı atmasın).
        var keepDeviceId = request.DeviceId ?? Guid.Empty;
        await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == userId && f.DeviceId != keepDeviceId && f.IsRevoked == false, cancellationToken);

        _logger.LogInformation("Parola kullanici tarafindan degistirildi: kullanici {UserId}", userId);
        return Result.Success();
    }
    #endregion

    #region Helpers
    /// <summary> boş kart id metni <c>null</c>'a çekilir(Db de veri varsa benzersiz olmlaı boş string çakışma yaratır dı) </summary>
    private static string? NormalizeIdentityCardId(string? identityCardId) => string.IsNullOrWhiteSpace(identityCardId) ? null : identityCardId.Trim();

    /// <summary> Kartin baska bir AKTIF kullanicida olup olmadigini kontrol eder yoksa çakışma oluşur </summary>
    private async Task<Result?> ValidateIdentityCardIdAsync(string? identityCardId, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        if (identityCardId == null)
            return null;

        var owner = await _unitOfWork.Users.GetAsync(
            select: u => new { u.Id, u.FullName },
            where: u => u.IsActive && u.IdentityCardId == identityCardId && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken: cancellationToken
        );

        if (owner == null)
            return null;

        string message = $"Bu kart numarası başka bir aktif kullanıcıya ({owner.FullName}) tanımlı.";
        return Result.Validation(new Dictionary<string, string[]> { [nameof(UserCreateDto.IdentityCardId)] = new[] { message } }, message: message);
    }
    #endregion
}