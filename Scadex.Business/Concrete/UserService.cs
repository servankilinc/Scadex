using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Model.Dtos.User.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly UserManager<User> _userManager;
    private readonly IMapper _mapper;
    private readonly IHttpContextManager _httpContextManager;
    public UserService(IUnitOfWork unitOfWork, IValidationService validationService, UserManager<User> userManager, IMapper mapper, IHttpContextManager httpContextManager)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _userManager = userManager;
        _mapper = mapper;
        _httpContextManager = httpContextManager;
    }

    #region Get
    public async Task<Result<User>> GetAsync(Expression<Func<User, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<User>.NotFound();
        return Result<User>.Success(result);
    }

    public async Task<Result<User>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<User>.NotFound();
        return Result<User>.Success(result);
    }

    public async Task<Result<UserBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAsync<UserBaseDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<UserBaseDto>.NotFound();
        return Result<UserBaseDto>.Success(result);
    }

    public async Task<Result<UserDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<UserDetailDto>.NotFound();
        return Result<UserDetailDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<User>>> GetListAsync(Expression<Func<User, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<User>>.NotFound();
        return Result<ICollection<User>>.Success(result);
    }

    public async Task<Result<ICollection<User>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<User>>.NotFound();
        return Result<ICollection<User>>.Success(result);
    }

    public async Task<Result<ICollection<UserBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAllAsync<UserBaseDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<UserBaseDto>>.NotFound();
        return Result<ICollection<UserBaseDto>>.Success(result);
    }

    public async Task<Result<ICollection<UserDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAllAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<UserDetailDto>>.NotFound();
        return Result<ICollection<UserDetailDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<User, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.Users.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.UserName ?? string.Empty }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
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

        if (!string.IsNullOrWhiteSpace(request.Email) && await _userManager.FindByEmailAsync(request.Email) != null)
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
    public async Task<Result<UserUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.GetAsync<UserUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<UserUpdateDto>.NotFound();
        return Result<UserUpdateDto>.Success(result);
    }

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

        // Pasife alinan kullanicinin refresh token'lari iptal edilir: yeniden aktiflestirildiginde eski oturumla geri donmesin.
        // Access token (24 sa) suresi dolana kadar gecerli kalir — istek basina IsActive kontrolu bilerek yok.
        if (isDeactivated)
            await _unitOfWork.RefreshTokens.RevokeDeviceRefreshTokensAsync(f => f.UserId == request.Id && f.IsRevoked == false, cancellationToken);

        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<UserDetailDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.PaginationAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: i => i.Include(x => x.Company), cancellationToken: cancellationToken);
        return Result<PaginationResponse<UserDetailDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<UserDetailDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.DatatableClientSideAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Company), cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<UserDetailDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<UserDetailDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Users.DatatableServerSideAsync<UserDetailDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Company), cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<UserDetailDto>>.Success(result);
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