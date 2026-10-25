using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.User.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

/// <summary>
/// Kullanıcı yönetimi. Kullanıcı bilgilerini okumak da yazmak da <c>ManageUsers</c> ister; 
/// kart tanımlama yalnızca kullanıcıları ilgili istek ile çekebilmeyi ve kart atama isteğini görür (<c>ManageAccessCards</c>).
/// </summary>
[EnableRateLimiting(RateLimiterKey.Default)]
public class UserController : BaseController
{
    private readonly IUserService _userService;
    public UserController(ILogger<UserController> logger, IUserService userService) : base(logger)
        => _userService = userService;

    #region List
    [HttpPost("list/detail")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> GetDetailList(DynamicRequest? request = default)
    {
        var result = await _userService.GetDetailListAsync(request);
        return ToAction(result);
    }

    /// <summary> Kart tanımlama penceresinin listesi: aktif kullanıcılar, yalnızca ad / kullanıcı adı / kart. </summary>
    [HttpGet("card-holders")]
    [RequirePermission(Permission.ManageAccessCards)]
    public async Task<IActionResult> GetCardHolders(CancellationToken cancellationToken)
    {
        var result = await _userService.GetCardHoldersAsync(cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Create
    [HttpPost]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Create(UserCreateDto request)
    {
        var result = await _userService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    [HttpPut]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> Update(UserUpdateDto request)
    {
        var result = await _userService.UpdateAsync(request);
        return ToAction(result);
    }

    /// <summary> Parolayı eskisini sormadan değiştirir (yönetici işlemi); kullanıcının açık oturumları yeniden giriş ister. </summary>
    [HttpPut("{id:guid}/password")]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> ChangePassword(Guid id, UserPasswordChangeDto request, CancellationToken cancellationToken)
    {
        var result = await _userService.ChangePasswordAsync(id, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary>
    /// Sahada okutulan kartı kullanıcıya atar (Kabinler → "Kart tanımlama"); yalnızca kart alanını yazar. Kullanıcı yönetiminden
    /// ayrı izindir: kartı tanımlayan kişinin kullanıcı oluşturması/rol vermesi gerekmez.
    /// </summary>
    [HttpPut("{id:guid}/identity-card")]
    [RequirePermission(Permission.ManageAccessCards)]
    public async Task<IActionResult> AssignIdentityCard(Guid id, UserIdentityCardAssignDto request)
    {
        var result = await _userService.AssignIdentityCardAsync(id, request);
        return ToAction(result);
    }
    #endregion
}
