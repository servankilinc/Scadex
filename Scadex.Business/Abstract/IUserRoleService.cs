using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.UserRole.Queries;

namespace Scadex.Business.Abstract;

public interface IUserRoleService
{
    Task<Result<ICollection<string>>> GetRolesOfUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ICollection<SelectItemDto>>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary> Kullanicinin rol kumesini verilen adlarla birebir esitler (ekle + cikar) </summary>
    Task<Result> SyncAsync(Guid userId, ICollection<string> roleNames, CancellationToken cancellationToken = default);

    // Role members — Roller ekranının "Üyeler" dialogu
    /// <summary> Role atanmış kullanıcılar (pasifler dahil). </summary>
    Task<Result<ICollection<RoleMemberDto>>> GetMembersAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary> Kullanıcıya tek bir rol ekler; zaten üyeyse işlem yapmaz. Pasif rol eklenemez (400). </summary>
    Task<Result> AddMemberAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary> Kullanıcıdan tek bir rolü çıkarır; üye değilse işlem yapmaz. </summary>
    Task<Result> RemoveMemberAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default);
}
