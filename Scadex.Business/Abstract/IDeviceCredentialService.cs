using Scadex.Core.Utils.ResultPattern;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Abstract;

/// <summary> Kimliği doğrulanan saha cihazı: kim olduğu ve hangi kabine yazdığı. </summary>
public sealed record AuthenticatedDevice(Guid DeviceId, Guid CabinetId);

/// <summary>
/// Saha cihazlarının kimliğini doğrular: kontrol kartı <c>DeviceId</c> header'ındaki MAC adresiyle, RemoteDesk PC'si Basic Auth ile
/// (kullanıcı adı cihazın Id'si, parola sistemin ürettiği sır — <c>DeviceCredential</c>). HTTP yüzeyi yoktur; yalnızca
/// <c>DeviceAuthentication</c>'daki handler'lar çağırır. Başarısızlıkta açıklama yalnızca günlük içindir (istemciye gitmez).
/// </summary>
public interface IDeviceCredentialService
{
    /// <summary> Cihaz aktif, şablonu <paramref name="deviceType"/> ve sırrı saklanan hash'le eşleşiyorsa başarılıdır. Yalnızca <see cref="DeviceType.Pc"/>. </summary>
    Task<Result<AuthenticatedDevice>> AuthenticateAsync(DeviceType deviceType, Guid deviceId, string secret, CancellationToken cancellationToken = default);

    /// <summary> Tek tip MAC adresi (<c>AA:BB:CC:DD:EE:FF</c>) aktif bir kontrol kartına aitse başarılıdır; birden fazla kartta varsa ilki. </summary>
    Task<Result<AuthenticatedDevice>> AuthenticateControlCardAsync(string macAddress, CancellationToken cancellationToken = default);
}
