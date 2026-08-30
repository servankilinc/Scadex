namespace Scadex.Core.Utils.Auth;

public static class AppClaimTypes
{
    /// <summary>Kullanicinin bagli oldugu sirket (tenant). Multi-tenant izolasyonun temeli.</summary>
    public const string CompanyId = "company_id";

    /// <summary>Tekil izin kodu (Permission.Code). Kullanici birden fazla tasiyabilir.</summary>
    public const string Permission = "permission";
}
