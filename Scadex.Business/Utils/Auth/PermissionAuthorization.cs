using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Core.Utils.Auth;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.Auth;

/// <summary>
/// Enpointi tek bir izne bağlar: policy adı izin kodudur (<c>nameof(Permission.X)</c>) ve token'daki <c>permission</c> claim'inden okunur. 
/// İzin yoksa 403. Policy'ler <see cref="PermissionAuthorization.AddPermissionPolicies"/> ile tek yerde kaydedilir.
/// <para>
/// Class düzeyindeki <c>[Authorize]</c>'larla VE ile birleşir: bir controller'a bu attribute'u class düzeyinde koymak o controller'daki HER endpointe izin şartı ekler.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(Permission permission) => Policy = permission.ToString();
}

public static class PermissionAuthorization
{
    /// <summary>
    /// <see cref="Permission"/>'ın her değeri için aynı adlı bir policy kaydeder. Modüller (RemoteDesk, Sinyalizasyon) de
    /// bu policy'leri adıyla kullanır; kendi kayıtlarında ayrıca tanımlamazlar.
    /// <para>
    /// Rol izni değişince kullanıcı yeniden giriş yapmalı ya da token'ı tazelenmeli — claim'ler token'a girişte yazılır.
    /// </para>
    /// </summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder();

        foreach (var permission in Enum.GetValues<Permission>())
        {
            string code = permission.ToString();
            builder.AddPolicy(code, policy => policy.RequireClaim(AppClaimTypes.Permission, code));
        }

        return services;
    }
}
