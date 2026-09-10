using Microsoft.AspNetCore.Identity;
using Scadex.Business.Abstract;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class UserRoleService : IUserRoleService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    public UserRoleService(UserManager<User> userManager, RoleManager<Role> roleManager, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ICollection<string>>> GetRolesOfUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result<ICollection<string>>.NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Result<ICollection<string>>.Success(roles);
    }

    public async Task<Result<ICollection<SelectItemDto>>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            return Result<ICollection<SelectItemDto>>.NotFound(message: "Rol bulunamadi.");

        var users = await _userManager.GetUsersInRoleAsync(roleName);
        ICollection<SelectItemDto> items = users
            .Select(u => new SelectItemDto { Value = u.Id.ToString(), Text = u.UserName ?? string.Empty })
            .ToList();

        return Result<ICollection<SelectItemDto>>.Success(items);
    }

    public async Task<Result<bool>> IsInRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result<bool>.NotFound();

        return Result<bool>.Success(await _userManager.IsInRoleAsync(user, roleName));
    }

    public async Task<Result> AssignAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result.NotFound(message: "Kullanici bulunamadi.");

        if (!await _roleManager.RoleExistsAsync(roleName))
            return Result.Failure($"'{roleName}' rolu tanimli degil.");

        if (await _userManager.IsInRoleAsync(user, roleName))
            return Result.Success(message: "Kullanici zaten bu role sahip.");

        var identityResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!identityResult.Succeeded)
            return Result.Failure(description: "Role cannot be assigned.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return Result.Success();
    }

    public async Task<Result> RemoveAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result.NotFound(message: "Kullanici bulunamadi.");

        if (!await _userManager.IsInRoleAsync(user, roleName))
            return Result.NotFound(message: "Kullanicinin boyle bir rolu yok.");

        var identityResult = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!identityResult.Succeeded)
            return Result.Failure(description: "Role cannot be removed.", metadata: GlobalExtensions.Meta("Identity Service Errors", identityResult.Errors));

        return Result.Success();
    }

    public async Task<Result> SyncAsync(Guid userId, ICollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result.NotFound(message: "Kullanici bulunamadi.");

        var requested = (roleNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Karsilastirma NormalizedName uzerinden: Identity rol adini da boyle arar, AuthService.GetPermissionCodesAsync da.
        var requestedKeys = requested.Select(name => name.ToUpperInvariant()).ToHashSet();
        var knownRoles = await _unitOfWork.Roles.GetAllAsync(where: r => r.NormalizedName != null && requestedKeys.Contains(r.NormalizedName), cancellationToken: cancellationToken) ?? new List<Role>();

        var unknown = requested.Where(name => !knownRoles.Any(r => r.NormalizedName == name.ToUpperInvariant())).ToArray();
        if (unknown.Length > 0)
        {
            var message = $"Tanımlı olmayan rol: {string.Join(", ", unknown)}";
            return Result.Validation(new Dictionary<string, string[]> { [nameof(roleNames)] = new[] { message } }, message: message);
        }

        var current = await _userManager.GetRolesAsync(user);
        var currentKeys = current.Select(name => name.ToUpperInvariant()).ToHashSet();

        var toAdd = knownRoles.Where(r => !currentKeys.Contains(r.NormalizedName!)).ToList();
        var toRemove = current.Where(name => !requestedKeys.Contains(name.ToUpperInvariant())).ToList();

        // Pasif rol YENIDEN atanamaz; zaten atanmis olan pasif rol ise listede kalabilir (cikarmak serbest).
        var passive = toAdd.Where(r => !r.IsActive).Select(r => r.Name).ToArray();
        if (passive.Length > 0)
        {
            var message = $"Pasif rol atanamaz: {string.Join(", ", passive)}";
            return Result.Validation(new Dictionary<string, string[]> { [nameof(roleNames)] = new[] { message } }, message: message);
        }

        if (toAdd.Count == 0 && toRemove.Count == 0)
            return Result.Success();

        // UserManager ile UoW ayni scoped AppDbContext'i paylasir; iki adim tek transaction'da kalir.
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            if (toRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removeResult.Succeeded)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(description: "Roles cannot be removed.", metadata: GlobalExtensions.Meta("Identity Service Errors", removeResult.Errors));
                }
            }

            if (toAdd.Count > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, toAdd.Select(r => r.Name!));
                if (!addResult.Succeeded)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(description: "Roles cannot be assigned.", metadata: GlobalExtensions.Meta("Identity Service Errors", addResult.Errors));
                }
            }

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
