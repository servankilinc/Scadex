using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Auth.Login;
using Scadex.Model.Auth.Logout;
using Scadex.Model.Auth.Refresh;
using Scadex.Model.Auth.SignUp;

namespace Scadex.Business.Abstract;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default);
    Task<Result<SignUpResponse>> SignUpAsync(SignUpRequest signUpRequest, CancellationToken cancellationToken = default);
    Task<Result<RefreshAuthResponse>> RefreshAsync(RefreshAuthRequest refreshAuthRequest, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutRequest logoutRequest, CancellationToken cancellationToken = default);

    /// <summary> Tüm refresh tokenları iptal eder. Kullanıcıyı tüm cihazlardan çıkış yaptırır. </summary>
    Task<Result> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}