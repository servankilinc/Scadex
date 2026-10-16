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

    // Profile — giriş yapan kullanıcının kendisi; `userId` her zaman token'dan gelir, gövdeden değil.
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary> Ad soyad, e-posta ve telefonu yazar; güncel profili döner. E-posta UserManager üzerinden yazılır (NormalizedEmail). </summary>
    Task<Result<UserProfileDto>> UpdateProfileAsync(Guid userId, UserProfileUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary> Mevcut parolayı doğrulayarak değiştirir; isteği gönderen cihaz dışındaki oturumları iptal eder. </summary>
    Task<Result> ChangeOwnPasswordAsync(Guid userId, UserOwnPasswordChangeDto request, CancellationToken cancellationToken = default);
}