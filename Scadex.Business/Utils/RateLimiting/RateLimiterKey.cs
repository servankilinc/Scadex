namespace Scadex.Business.Utils.RateLimiting;

/// <summary>
/// Rate limit politika adları. Politikaların kendisi <c>Program.cs</c>'te (<c>AddRateLimiter</c>) kaydedilir; adlar Business'tadır ki müşteri
/// modülleri de (WebAPI'ye referans veremezler) aynı politikayı takabilsin. Burada olup <c>Program.cs</c>'te kaydı olmayan bir ad takılırsa o uç HER istekte 500 döner.
/// </summary>
public static class RateLimiterKey
{
    /// <summary> Kullanıcı başına (kimliksizse IP başına) — çekirdek ve modül controller'larının varsayılanı. </summary>
    public const string Default = "policy_rate_limiter";

    /// <summary> SCADA ingest / kart okuma — IP başına, daha geniş. </summary>
    public const string Scada = "policy_rate_limiter_scada";

    /// <summary> MediaMTX'in yetki kancası — IP başına, daha geniş. </summary>
    public const string MediaGateway = "policy_rate_limiter_media_gateway";
}
