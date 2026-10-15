using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Model.Dtos.User.Queries;

namespace Scadex.Business.Abstract;

public interface IUserService
{
    // List
    Task<Result<ICollection<UserDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    /// <summary> Kart tanımlama penceresi için aktif kullanıcılar (isim sırasıyla), yalnızca kart atamaya yeterli alanlar. </summary>
    Task<Result<ICollection<UserCardHolderDto>>> GetCardHoldersAsync(CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(UserCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result> UpdateAsync(UserUpdateDto request, CancellationToken cancellationToken = default);
    Task<Result> AssignIdentityCardAsync(Guid userId, UserIdentityCardAssignDto request, CancellationToken cancellationToken = default);

    /// <summary> Parolayı eskisini sormadan değiştirir; refresh token'ları iptal eder, kilidi sıfırlar. </summary>
    Task<Result> ChangePasswordAsync(Guid userId, UserPasswordChangeDto request, CancellationToken cancellationToken = default);
}