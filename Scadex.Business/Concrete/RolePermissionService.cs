using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.RolePermission.Commands;
using Scadex.Model.Dtos.RolePermission.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class RolePermissionService : IRolePermissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public RolePermissionService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<RolePermissionDto>>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAllAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.RoleId == roleId, cancellationToken: cancellationToken);
        return Result<ICollection<RolePermissionDto>>.Success(result ?? new List<RolePermissionDto>());
    }

    /// <inheritdoc />
    public async Task<Result> SyncRolePermissionsAsync(Guid roleId, ICollection<int> permissionIds, CancellationToken cancellationToken = default)
    {
        var roleExists = await _unitOfWork.Roles.IsExistAsync(where: f => f.Id == roleId, cancellationToken: cancellationToken);
        if (!roleExists)
            return Result.NotFound(message: "The specified role was not found.");

        var requested = (permissionIds ?? Array.Empty<int>()).Distinct().ToList();

        // Gonderilen izinlerin hepsi gercekten var mi?
        if (requested.Count > 0)
        {
            var knownPermissions = await _unitOfWork.Permissions.GetAllAsync(select: p => p.Id, where: p => requested.Contains(p.Id), cancellationToken: cancellationToken);
            var unknown = requested.Except(knownPermissions ?? new List<int>()).ToArray();
            if (unknown.Length > 0)
                return Result.Failure($"Tanımlı olamayan izin bilgileri gönderildiği için işleme devam edilemiyor", $"id(s): {string.Join(", ", unknown)}");
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var current = await _unitOfWork.RolePermissions.GetAllAsync(where: f => f.RoleId == roleId, cancellationToken: cancellationToken) ?? new List<RolePermission>();
            var currentIds = current.Select(f => f.PermissionId).ToHashSet();

            var toAdd = requested.Where(id => !currentIds.Contains(id)).Select(id => new RolePermission { RoleId = roleId, PermissionId = id }).ToList();
            var toRemove = current.Where(f => !requested.Contains(f.PermissionId)).ToList();

            if (toRemove.Count > 0)
                await _unitOfWork.RolePermissions.DeleteAndSaveAsync(toRemove, cancellationToken);
            if (toAdd.Count > 0)
                await _unitOfWork.RolePermissions.AddAndSaveAsync(toAdd, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }


    #region Get
    public async Task<Result<RolePermission>> GetAsync(Expression<Func<RolePermission, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<RolePermission>.NotFound();
        return Result<RolePermission>.Success(result);
    }

    public async Task<Result<RolePermission>> GetAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAsync(where: (f) => f.PermissionId == permissionId && f.RoleId == roleId, cancellationToken: cancellationToken);
        if (result == null)
            return Result<RolePermission>.NotFound();
        return Result<RolePermission>.Success(result);
    }

    public async Task<Result<RolePermissionDto>> GetBaseAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.PermissionId == permissionId && f.RoleId == roleId, cancellationToken: cancellationToken);
        if (result == null)
            return Result<RolePermissionDto>.NotFound();
        return Result<RolePermissionDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<RolePermission>>> GetListAsync(Expression<Func<RolePermission, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<RolePermission>>.NotFound();
        return Result<ICollection<RolePermission>>.Success(result);
    }

    public async Task<Result<ICollection<RolePermission>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<RolePermission>>.NotFound();
        return Result<ICollection<RolePermission>>.Success(result);
    }

    public async Task<Result<ICollection<RolePermissionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.GetAllAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<RolePermissionDto>>.NotFound();
        return Result<ICollection<RolePermissionDto>>.Success(result);
    }
    #endregion

    #region Create
    public async Task<Result> CreateAsync(RolePermissionCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: $"Validation failed for RolePermissionCreateDto");

        // Bilesik anahtar {RoleId, PermissionId} oldugu icin ayni satiri ikinci kez eklemek
        // veritabani seviyesinde PK ihlaliyle 500 uretir; burada 400 dondurulur.
        var alreadyExists = await _unitOfWork.RolePermissions.IsExistAsync(where: f => f.RoleId == request.RoleId && f.PermissionId == request.PermissionId, cancellationToken: cancellationToken);
        if (alreadyExists)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.PermissionId)] = new[] { "This permission is already assigned to the role." } }, message: "This permission is already assigned to the role.");

        var roleExists = await _unitOfWork.Roles.IsExistAsync(where: f => f.Id == request.RoleId, cancellationToken: cancellationToken);
        if (!roleExists)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.RoleId)] = new[] { "The specified role was not found." } }, message: "The specified role was not found.");

        var permissionExists = await _unitOfWork.Permissions.IsExistAsync(where: f => f.Id == request.PermissionId, cancellationToken: cancellationToken);
        if (!permissionExists)
            return Result.Validation(new Dictionary<string, string[]> { [nameof(request.PermissionId)] = new[] { "The specified permission was not found." } }, message: "The specified permission was not found.");

        await _unitOfWork.RolePermissions.AddAndSaveAsync(_mapper.Map<RolePermission>(request), cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Delete
    public async Task<Result> DeleteAsync(int permissionId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var affected = await _unitOfWork.RolePermissions.DeleteAndSaveAsync(where: (f) => f.PermissionId == permissionId && f.RoleId == roleId, cancellationToken);
        if (affected == 0)
            return Result.NotFound();
        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<RolePermissionDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.PaginationAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<PaginationResponse<RolePermissionDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<RolePermissionDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.DatatableClientSideAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<RolePermissionDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<RolePermissionDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.RolePermissions.DatatableServerSideAsync<RolePermissionDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<RolePermissionDto>>.Success(result);
    }
    #endregion
}
