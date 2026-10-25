using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile;

/// <summary>
/// Kameraya bağlanmak için gereken her şey, TEK bir düz kayıtta. Kamera 2026-10-22'den beri <c>Device</c> + 1-1
/// <c>Camera</c> ek satırıdır: IP cihazda, marka ve kimlik bilgileri ek satırdadır. RTSP adresi kuran kod
/// (<see cref="ICameraProtocolProfile"/>, MediaMTX geçidi, FFmpeg) entity yerine bunu alır — navigasyonun
/// <c>Include</c> edilmesini unutmak sessizce boş IP'li bir adres üretmesin diye; sorgu bunu doğrudan projeksiyonla doldurur.
/// İçinde parola vardır: istemciye gönderilmez, loglanmaz.
/// </summary>
public sealed record CameraEndpoint(Guid Id, string IpAddress, CameraBrand Brand, string? Username, string? Password);
