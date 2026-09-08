using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Datatable;
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
    public UserService(IUnitOfWork unitOfWork, IValidationService validationService, UserManager<User> userManager, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _userManager = userManager;
        _mapper = mapper;
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
        var entity = await _unitOfWork.Users.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();
        await _unitOfWork.Users.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);
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
}