using Scadex.Core.Utils.ResultPattern;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Abstract;

/// <summary> Basic Auth ile doğrulanan saha cihazı: kim olduğu ve hangi kabine yazdığı. </summary>
public sealed record AuthenticatedDevice(Guid DeviceId, Guid CabinetId);

/// <summary>
/// Saha cihazının (kontrol kartı, RemoteDesk PC'si) Basic Auth kimliğini doğrular.
/// Kullanıcı adı cihazın Id'si, parola sistemin ürettiği sırdır (<c>DeviceCredential</c>). HTTP yüzeyi yoktur; yalnızca
/// <c>DeviceBasicAuthenticationHandler</c> çağırır.
/// </summary>
public interface IDeviceCredentialService
{
    /// <summary>
    /// Cihaz aktif, şablonu <paramref name="deviceType"/> ve sırrı saklanan hash'le eşleşiyorsa başarılıdır. Başarısızlıkta açıklama
    /// yalnızca günlük içindir (istemciye gitmez — hangi adımda düştüğünü söylemek kimlik avına yardım ederdi).
    /// </summary>
    Task<Result<AuthenticatedDevice>> AuthenticateAsync(DeviceType deviceType, Guid deviceId, string secret, CancellationToken cancellationToken = default);
}
