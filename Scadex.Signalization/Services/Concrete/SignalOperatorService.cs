using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Signalization.Data;
using Scadex.Signalization.Dtos.Operator.Commands;
using Scadex.Signalization.Dtos.Operator.Queries;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Services.Concrete;

/// <summary>
/// Operator = mevcut <c>User</c>. Ayri bir operator tablosu ve ayri bir yetki yapisi yoktur: kurum bir roldur,
/// kart numarasi <c>User.IdentityCardId</c>'dir (cekirdek kullanici ekraninda yazilir).
/// </summary>
public class SignalOperatorService : ISignalOperatorService
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRoleService _userRoleService;

    public SignalOperatorService(SignalizationDbContext db, IUnitOfWork unitOfWork, IUserRoleService userRoleService)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _userRoleService = userRoleService;
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<SignalOperatorDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllAsync(
            select: u => new { u.Id, u.FullName, u.UserName, u.IdentityCardId },
            where: u => u.IsActive,
            cancellationToken: cancellationToken) ?? [];

        var authorities = await _db.Authorities.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var roleNames = await LoadRoleNamesAsync(authorities.Select(a => a.RoleId), cancellationToken);

        // Kurum basina rol uyeleri: kurum sayisi kadar sorgu (kullanici basina degil).
        var authoritiesByUser = new Dictionary<Guid, List<(Guid Id, string Name)>>();
        foreach (var authority in authorities)
        {
            if (!roleNames.TryGetValue(authority.RoleId, out var roleName))
                continue;

            var members = await _userRoleService.GetUsersInRoleAsync(roleName, cancellationToken);
            if (!members.IsSuccess)
                continue;

            foreach (var member in members.Data)
            {
                if (!Guid.TryParse(member.Value, out var memberId))
                    continue;

                if (!authoritiesByUser.TryGetValue(memberId, out var list))
                    authoritiesByUser[memberId] = list = [];
                list.Add((authority.Id, authority.Name));
            }
        }

        ICollection<SignalOperatorDto> result = users
            .OrderBy(u => u.FullName)
            .Select(u =>
            {
                authoritiesByUser.TryGetValue(u.Id, out var owned);
                return new SignalOperatorDto
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    UserName = u.UserName,
                    IdentityCardId = u.IdentityCardId,
                    AuthorityId = owned?.Count == 1 ? owned[0].Id : null,
                    AuthorityName = owned == null ? null : string.Join(", ", owned.Select(o => o.Name)),
                    HasMultipleAuthorities = owned?.Count > 1
                };
            })
            .ToList();

        return Result<ICollection<SignalOperatorDto>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result> SetAuthorityAsync(Guid userId, SignalOperatorAuthorityRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetAsync(select: u => new { u.Id }, where: u => u.Id == userId && u.IsActive, cancellationToken: cancellationToken);
        if (user == null)
            return Result.NotFound(message: "Kullanıcı bulunamadı veya pasif durumda.");

        var authorities = await _db.Authorities.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var roleNames = await LoadRoleNamesAsync(authorities.Select(a => a.RoleId), cancellationToken);

        string? targetRoleName = null;
        if (request.AuthorityId is Guid authorityId)
        {
            var target = authorities.FirstOrDefault(a => a.Id == authorityId);
            if (target == null || !roleNames.TryGetValue(target.RoleId, out targetRoleName))
            {
                const string message = "Kurum bulunamadı veya pasif durumda.";
                return Result.Validation(new Dictionary<string, string[]> { [nameof(request.AuthorityId)] = [message] }, message: message);
            }
        }

        var current = await _userRoleService.GetRolesOfUserAsync(userId, cancellationToken);
        if (!current.IsSuccess)
            return Result.NotFound(message: "Kullanıcının rolleri okunamadı.");

        // Kurum disi roller korunur; TUM kurum rolleri cikarilir ve (varsa) secilen eklenir — tek kurum kurali kaynaginda.
        var authorityRoleKeys = roleNames.Values.Select(n => n.ToUpperInvariant()).ToHashSet();
        var next = current.Data.Where(name => !authorityRoleKeys.Contains(name.ToUpperInvariant())).ToList();
        if (targetRoleName != null)
            next.Add(targetRoleName);

        return await _userRoleService.SyncAsync(userId, next, cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadRoleNamesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();
        var roles = await _unitOfWork.Roles.GetAllAsync(
            select: r => new { r.Id, r.Name },
            where: r => ids.Contains(r.Id),
            cancellationToken: cancellationToken) ?? [];

        return roles.Where(r => r.Name != null).ToDictionary(r => r.Id, r => r.Name!);
    }
}
