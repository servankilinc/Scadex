using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.Auth;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.Auth;

// Saha cihazlarinin (kontrol karti, RemoteDesk PC'si) HTTP Basic Auth kimligi.
// Kullanici adi cihazin Id'si, parola sistemin urettigi sir (Scadex.Core/Utils/DeviceCredential.cs).

/// <summary> Cihaz kimliği şemaları. Her şema tek bir cihaz tipini kabul eder: kart sırrı PcHub'a, PC sırrı ingest'e girmez. </summary>
public static class DeviceAuthSchemes
{
    public const string ControlCard = "ControlCardBasic";
    public const string Pc = "PcBasic";
}

/// <summary>
/// Uçların kullandığı policy adları — şema + <c>device_type</c> claim'i. Claim şartı, policy bir controller'ın sınıf düzeyindeki
/// <c>[Authorize]</c>'ıyla VE ile birleştiğinde de kullanıcı JWT'sini dışarıda tutar.
/// </summary>
public static class DeviceAuthPolicies
{
    public const string ControlCard = "Device.ControlCard";
    public const string Pc = "Device.Pc";
}

public sealed class DeviceBasicAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary> Bu şemanın kabul ettiği cihaz tipi. </summary>
    public DeviceType DeviceType { get; set; }
}

/// <summary>
/// <c>Authorization: Basic base64(deviceId:sır)</c>. Başlık yoksa ya da başka şemaysa (Bearer) karışmaz (<c>NoResult</c>).
/// Sır asla loglanmaz; reddedilen deneme cihaz Id'si + IP ile <c>Warning</c>. Reddin nedeni istemciye söylenmez.
/// </summary>
public sealed class DeviceBasicAuthenticationHandler : AuthenticationHandler<DeviceBasicAuthenticationOptions>
{
    private const string BasicScheme = "Basic";

    private readonly IDeviceCredentialService _credentials;

    public DeviceBasicAuthenticationHandler(IOptionsMonitor<DeviceBasicAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IDeviceCredentialService credentials) : base(options, logger, encoder)
    {
        _credentials = credentials;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !BasicScheme.Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(header.Parameter))
            return AuthenticateResult.NoResult();

        string decoded;
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter)); }
        catch (FormatException) { return Reject(null, "Basic başlığı çözülemedi"); }

        int colon = decoded.IndexOf(':');
        if (colon < 0 || !Guid.TryParse(decoded.AsSpan(0, colon), out var deviceId))
            return Reject(null, "Kullanıcı adı bir cihaz Id'si değil");

        var result = await _credentials.AuthenticateAsync(Options.DeviceType, deviceId, decoded[(colon + 1)..], Context.RequestAborted);
        if (!result.IsSuccess)
            return Reject(deviceId, result.Error.Description);

        var device = result.Data;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.DeviceId.ToString()),
            new Claim(AppClaimTypes.DeviceType, ((int)Options.DeviceType).ToString()),
            new Claim(AppClaimTypes.CabinetId, device.CabinetId.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <summary> 401 + <c>WWW-Authenticate: Basic</c>. Eklenir (yazılmaz): aynı istekte JWT şeması da kendi başlığını koyabilir. </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Basic realm=\"Scadex\", charset=\"UTF-8\"");
        return Task.CompletedTask;
    }

    private AuthenticateResult Reject(Guid? deviceId, string reason)
    {
        Logger.LogWarning("Cihaz kimliği reddedildi ({Scheme}): cihaz {DeviceId}, {Ip} — {Reason}",
            Scheme.Name, deviceId?.ToString() ?? "?", Context.Connection.RemoteIpAddress?.ToString() ?? "?", reason);
        return AuthenticateResult.Fail("Cihaz kimliği reddedildi");
    }
}

public static class DeviceAuthentication
{
    /// <summary>
    /// İki şemayı ve iki policy'yi kaydeder (<c>Program.cs</c>, <c>.AddJwtBearer(...)</c> zincirinde). PC tipi çekirdektedir
    /// (<c>DeviceType.Pc</c>, <c>PcDevice</c>), bu yüzden RemoteDesk modülü kendi şemasını kaydetmez, policy adını kullanır.
    /// </summary>
    public static AuthenticationBuilder AddDeviceAuthentication(this AuthenticationBuilder builder)
    {
        builder.AddScheme<DeviceBasicAuthenticationOptions, DeviceBasicAuthenticationHandler>(DeviceAuthSchemes.ControlCard, o => o.DeviceType = DeviceType.ControlModule);
        builder.AddScheme<DeviceBasicAuthenticationOptions, DeviceBasicAuthenticationHandler>(DeviceAuthSchemes.Pc, o => o.DeviceType = DeviceType.Pc);

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DeviceAuthPolicies.ControlCard, policy => policy
                .AddAuthenticationSchemes(DeviceAuthSchemes.ControlCard)
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.DeviceType, ((int)DeviceType.ControlModule).ToString()))
            .AddPolicy(DeviceAuthPolicies.Pc, policy => policy
                .AddAuthenticationSchemes(DeviceAuthSchemes.Pc)
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.DeviceType, ((int)DeviceType.Pc).ToString()));

        return builder;
    }

    /// <summary> Cihaz kimliğiyle doğrulanmış isteğin cihaz Id'si (yoksa <c>null</c>). </summary>
    public static Guid? GetDeviceId(this ClaimsPrincipal principal) =>
        principal.HasClaim(c => c.Type == AppClaimTypes.DeviceType)
        && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary> Cihaz kimliğiyle doğrulanmış isteğin kabini (yoksa <c>null</c>). </summary>
    public static Guid? GetDeviceCabinetId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(AppClaimTypes.CabinetId), out var id) ? id : null;
}
