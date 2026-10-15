using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.Model.Dtos.Role.Queries;
using Scadex.Model.Entities;

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

    #region List
    public async Task<Result<ICollection<RoleDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Roles.GetAllAsync<RoleDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<RoleDto>>.NotFound();
        return Result<ICollection<RoleDto>>.Success(result);
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
}