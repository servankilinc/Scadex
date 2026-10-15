using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.RolePermission.Queries;
using Scadex.Model.Entities;

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
}
