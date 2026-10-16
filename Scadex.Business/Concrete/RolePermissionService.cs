using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.RolePermission.Commands;
using Scadex.Model.Dtos.RolePermission.Queries;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class RolePermissionService : IRolePermissionService
{
    private const string PermissionIdsField = "PermissionIds";

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
    public async Task<Result<ICollection<RolePermissionMatrixItemDto>>> GetMatrixAsync(CancellationToken cancellationToken = default)
    {
        var roleIds = await _unitOfWork.Roles.GetAllAsync(select: r => r.Id, cancellationToken: cancellationToken) ?? new List<Guid>();
        var pairs = await _unitOfWork.RolePermissions.GetAllAsync(select: rp => new { rp.RoleId, rp.PermissionId }, cancellationToken: cancellationToken);

        // İzinsiz rol de satır olarak döner: matris her rolü bir sütun olarak çizer.
        var byRole = pairs == null
            ? new Dictionary<Guid, List<int>>()
            : pairs.GroupBy(p => p.RoleId).ToDictionary(g => g.Key, g => g.Select(p => p.PermissionId).OrderBy(id => id).ToList());
        ICollection<RolePermissionMatrixItemDto> matrix = roleIds
            .Select(roleId => new RolePermissionMatrixItemDto { RoleId = roleId, PermissionIds = byRole.GetValueOrDefault(roleId) ?? new List<int>() })
            .ToList();

        return Result<ICollection<RolePermissionMatrixItemDto>>.Success(matrix);
    }

    /// <inheritdoc />
    public Task<Result> SyncRolePermissionsAsync(Guid roleId, ICollection<int> permissionIds, CancellationToken cancellationToken = default)
        => SyncManyAsync(new Dictionary<Guid, ICollection<int>> { [roleId] = permissionIds ?? Array.Empty<int>() }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> SaveMatrixAsync(ICollection<RolePermissionMatrixSaveItem> items, CancellationToken cancellationToken = default)
    {
        items ??= Array.Empty<RolePermissionMatrixSaveItem>();
        foreach (var item in items)
        {
            var validationResult = await _validationService.ValidateAsync(item, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Validation(validationResult.Failures);
        }

        var duplicated = items.GroupBy(i => i.RoleId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicated.Length > 0)
        {
            var message = $"Aynı rol birden fazla kez gönderildi: {string.Join(", ", duplicated)}";
            return Result.Validation(new Dictionary<string, string[]> { [PermissionIdsField] = new[] { message } }, message: message);
        }

        if (items.Count == 0)
            return Result.Success();

        return await SyncManyAsync(items.ToDictionary(i => i.RoleId, i => i.PermissionIds), cancellationToken);
    }

    /// <summary>
    /// Verilen her rolün izin kümesini birebir eşitler — hepsi TEK transaction'da: matris ya tamamen yazılır ya hiç.
    /// </summary>
    private async Task<Result> SyncManyAsync(IDictionary<Guid, ICollection<int>> requestedByRole, CancellationToken cancellationToken)
    {
        var roleIds = requestedByRole.Keys.ToList();
        var knownRoleIds = await _unitOfWork.Roles.GetAllAsync(select: r => r.Id, where: r => roleIds.Contains(r.Id), cancellationToken: cancellationToken) ?? new List<Guid>();
        var unknownRoles = roleIds.Except(knownRoleIds).ToArray();
        if (unknownRoles.Length > 0)
            return Result.NotFound(message: $"Rol bulunamadı: {string.Join(", ", unknownRoles)}");

        var requested = requestedByRole.ToDictionary(kv => kv.Key, kv => (kv.Value ?? Array.Empty<int>()).Distinct().ToHashSet());

        // Gönderilen izinlerin hepsi katalogda var mı? Yanlış kimlik istemci hatasıdır (400), sunucu hatası değil.
        var allRequested = requested.Values.SelectMany(ids => ids).Distinct().ToList();
        if (allRequested.Count > 0)
        {
            var knownPermissions = await _unitOfWork.Permissions.GetAllAsync(select: p => p.Id, where: p => allRequested.Contains(p.Id), cancellationToken: cancellationToken) ?? new List<int>();
            var unknown = allRequested.Except(knownPermissions).ToArray();
            if (unknown.Length > 0)
            {
                var message = $"Tanımlı olmayan izin: {string.Join(", ", unknown)}";
                return Result.Validation(new Dictionary<string, string[]> { [PermissionIdsField] = new[] { message } }, message: message);
            }
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var current = await _unitOfWork.RolePermissions.GetAllAsync(where: f => roleIds.Contains(f.RoleId), cancellationToken: cancellationToken) ?? new List<RolePermission>();
            var currentByRole = current.GroupBy(f => f.RoleId).ToDictionary(g => g.Key, g => g.ToList());

            var toAdd = new List<RolePermission>();
            var toRemove = new List<RolePermission>();
            foreach (var (roleId, wanted) in requested)
            {
                var existing = currentByRole.GetValueOrDefault(roleId) ?? new List<RolePermission>();
                var existingIds = existing.Select(f => f.PermissionId).ToHashSet();
                toAdd.AddRange(wanted.Where(id => !existingIds.Contains(id)).Select(id => new RolePermission { RoleId = roleId, PermissionId = id }));
                toRemove.AddRange(existing.Where(f => !wanted.Contains(f.PermissionId)));
            }

            if (toAdd.Count == 0 && toRemove.Count == 0)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Success();
            }

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
