namespace Scadex.Core.Utils.Auth;

public static class AppClaimTypes
{
    /// <summary>Kullanicinin bagli oldugu sirket (tenant). Multi-tenant izolasyonun temeli.</summary>
    public const string CompanyId = "company_id";

    /// <summary>Tekil izin kodu (Permission.Code). Kullanici birden fazla tasiyabilir.</summary>
    public const string Permission = "permission";

    /// <summary>Basic Auth ile dogrulanan saha cihazinin tipi (<c>DeviceType</c>, sayi). Kullanici token'inda YOKTUR.</summary>
    public const string DeviceType = "device_type";

    /// <summary>Basic Auth ile dogrulanan saha cihazinin kabini — ingest ve kart okuma kabini buradan alir.</summary>
    public const string CabinetId = "cabinet_id";
}
