using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Signalization.Data;
using Scadex.Signalization.Dtos.Authority.Commands;
using Scadex.Signalization.Dtos.Authority.Queries;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Services.Concrete;

public class SignalAuthorityService : ISignalAuthorityService
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;

    public SignalAuthorityService(SignalizationDbContext db, IUnitOfWork unitOfWork, IValidationService validationService)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validationService = validationService;
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<SignalAuthorityDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var authorities = await _db.Authorities.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var roleIds = authorities.Select(a => a.RoleId).Distinct().ToList();

        var roles = await _unitOfWork.Roles.GetAllAsync(
            select: r => new { r.Id, r.Name, r.IsActive },
            where: r => roleIds.Contains(r.Id),
            cancellationToken: cancellationToken) ?? [];
        var roleById = roles.ToDictionary(r => r.Id);

        ICollection<SignalAuthorityDto> list = authorities.Select(a => new SignalAuthorityDto
        {
            Id = a.Id,
            Name = a.Name,
            RoleId = a.RoleId,
            RoleName = roleById.TryGetValue(a.RoleId, out var role) ? role.Name : null,
            RoleIsActive = roleById.TryGetValue(a.RoleId, out var r2) && r2.IsActive,
            IsActive = a.IsActive
        }).ToList();

        return Result<ICollection<SignalAuthorityDto>>.Success(list);
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(SignalAuthoritySaveRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for SignalAuthoritySaveRequest");

        // 2) Roller cekirdekte var mi? (FK yok — farkli context)
        var requestedRoleIds = request.Authorities.Select(a => a.RoleId).Distinct().ToList();
        var existingRoleIds = await _unitOfWork.Roles.GetAllAsync(
            select: r => r.Id,
            where: r => requestedRoleIds.Contains(r.Id),
            cancellationToken: cancellationToken) ?? [];

        var errors = new Dictionary<string, string[]>();
        for (int i = 0; i < request.Authorities.Count; i++)
        {
            if (!existingRoleIds.Contains(request.Authorities[i].RoleId))
                errors[$"Authorities[{i}].RoleId"] = ["Rol bulunamadı."];
        }

        // 3) Pasife alinacak kurum aktif bir ic kapida kullaniliyor mu? Kullaniliyorsa kapi sahipsiz kalirdi.
        var existing = await _db.Authorities.ToListAsync(cancellationToken);
        var requestedIds = request.Authorities.Select(a => a.Id).ToHashSet();
        var toDeactivate = existing.Where(a => a.IsActive && !requestedIds.Contains(a.Id)).ToList();

        foreach (var authority in toDeactivate)
        {
            bool inUse = await _db.InnerDoors.AnyAsync(i => i.IsActive && i.AuthorityId == authority.Id && i.OuterDoor!.IsActive, cancellationToken);
            if (inUse)
                errors[nameof(request.Authorities)] = [$"'{authority.Name}' kurumu aktif bir iç kapıda kullanılıyor; önce kapı yapılandırmasından çıkarın."];
        }

        if (errors.Count > 0)
            return Result.Validation(errors, message: errors.Values.First()[0]);

        // 4) Esitle: once pasife almalar, sonra yazmalar — filtreli tekil indeksler (RoleId, Name) ayni batch'te
        //    siralama garantisi olmadigi icin cakisabilirdi (DiagramService.SaveAsync ile ayni gerekce).
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var authority in toDeactivate)
            authority.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var draft in request.Authorities)
        {
            var entity = existing.FirstOrDefault(a => a.Id == draft.Id);
            if (entity == null)
            {
                _db.Authorities.Add(new SignalAuthority { Id = draft.Id, Name = draft.Name.Trim(), RoleId = draft.RoleId, IsActive = true });
                continue;
            }

            entity.Name = draft.Name.Trim();
            entity.RoleId = draft.RoleId;
            entity.IsActive = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
