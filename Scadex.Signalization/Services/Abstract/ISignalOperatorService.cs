using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Signalization.Model.Dtos.Operator.Commands;
using Scadex.Signalization.Model.Dtos.Operator.Queries;

namespace Scadex.Signalization.Services.Abstract;

public interface ISignalOperatorService
{
    /// <summary> Aktif kullanicilar, kart numarasi ve roluunden turetilen kurumuyla. </summary>
    Task<Result<ICollection<SignalOperatorDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanicinin TEK kurumunu belirler: diger kurum rolleri cikarilir, secilen eklenir, kurum disi roller korunur.
    /// Yazim cekirdegin rol sync'i uzerinden gider (<c>IUserRoleService.SyncAsync</c>) — UserRoles'a ikinci yazim yolu acilmaz.
    /// </summary>
    Task<Result> SetAuthorityAsync(Guid userId, SignalOperatorAuthorityRequest request, CancellationToken cancellationToken = default);

    /// <summary> Sahada okutulan kartı kullanıcıya tanıtır. Scadex çekirdeğin  <c>IUserService.AssignIdentityCardAsync</c>'i üzerinden gider </summary>
    Task<Result> AssignCardAsync(Guid userId, UserIdentityCardAssignDto request, CancellationToken cancellationToken = default);
}
