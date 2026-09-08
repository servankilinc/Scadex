using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.Model.Dtos.Role.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly RoleManager<Role> _roleManager;
    private readonly IMapper _mapper;
    public RoleService(IUnitOfWork unitOfWork, IValidationService validationService, RoleManager<Role> roleManager, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _roleManager = roleManager;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Role>> GetAsync(Expression<Func<Role, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Role>.NotFound();
        return Result<Role>.Success(result);
    }

    public async Task<Result<Role>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Role>.NotFound();
        return Result<Role>.Success(result);
    }

    public async Task<Result<RoleDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<RoleDto>.NotFound();
        return Result<RoleDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Role>>> GetListAsync(Expression<Func<Role, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Role>>.NotFound();
        return Result<ICollection<Role>>.Success(result);
    }

    public async Task<Result<ICollection<Role>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Role>>.NotFound();
        return Result<ICollection<Role>>.Success(result);
    }

    public async Task<Result<ICollection<RoleDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAllAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<RoleDto>>.NotFound();
        return Result<ICollection<RoleDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Role, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.Roles.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.Name ?? string.Empty }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
    }
    #endregion

    #region Create
    public async Task<Result> CreateAsync(RoleCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: $"Validation failed for RoleCreateDto");

        if (await _roleManager.RoleExistsAsync(request.Name))
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.Name)] = new[] { "Bu rol adi zaten kullaniliyor." } }, message: "Bu rol adi zaten kullaniliyor.");

        var role = _mapper.Map<Role>(request);
        var identityResult = await _roleManager.CreateAsync(role);
        if (!identityResult.Succeeded)
            return Result.Failure(description: "Role cannot be created.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return Result.Success();
    }
    #endregion

    #region Update
    public async Task<Result<RoleUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAsync<RoleUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<RoleUpdateDto>.NotFound();
        return Result<RoleUpdateDto>.Success(result);
    }

    public async Task<Result> UpdateAsync(RoleUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);
        var entity = await _roleManager.FindByIdAsync(request.Id.ToString());
        if (entity == null)
            return Result.NotFound();

        if (entity.IsImmutable)
            return Result.Forbidden(message: "Bu rol sistem tarafindan kilitlenmistir ve degistirilemez.");

        // RoleManager uzerinden guncellenir ki isim degisikliginde NormalizedName de yenilensin.
        var identityResult = await _roleManager.UpdateAsync(_mapper.Map(request, entity));
        if (!identityResult.Succeeded)
            return Result.Failure(description: "Role cannot be updated.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<RoleDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.PaginationAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<PaginationResponse<RoleDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<RoleDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.DatatableClientSideAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<RoleDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<RoleDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.DatatableServerSideAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<RoleDto>>.Success(result);
    } 
    #endregion
}