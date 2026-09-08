namespace Scadex.WebAPI.Tools;

public static class RateLimiterKey
{
    public const string Default = "policy_rate_limiter";
    public const string Scada = "policy_rate_limiter_scada";
    public const string MediaGateway = "policy_rate_limiter_media_gateway";
}
