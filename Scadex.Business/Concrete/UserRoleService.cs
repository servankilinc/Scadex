using Microsoft.AspNetCore.Identity;
using Scadex.Business.Abstract;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class UserRoleService : IUserRoleService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    public UserRoleService(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
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
}
