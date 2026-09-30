using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;

namespace Scadex.RemoteDesk.Media;

/// <summary>
/// PC pathlerinin tokenları. Token RTSP/WHEP parolasıdır; MediaMTX onu auth kancasına iletir.
/// <list type="bullet">
/// <item><b>Okuma toekn'ı</b> — tarayıcı; kısa ömürlü, WHEP el sıkışmasında bir kez doğrulanır.</item>
/// <item><b>Yayın toekn'ı</b> — Windows istemcisi; oturum boyunca geçerli, TEK KULLANIMLIK DEĞİL (FFmpeg koparsa yeniden bağlanır, MediaMTX tekrar sorar).
///  Oturum bitince <see cref="RevokePublishAsync"/> ile silinir; üst sınır yalnızca sahipsiz kalana karşı emniyettir.</item>
/// </list>
/// Anahtar uzayları ayrıdır: okuma toekn'ı yayın, yayın token'ı okuma yapmaz; kameranın tokenları da karışmaz.
/// </summary>
public sealed class ScreenTicketStore
{
    private static readonly TimeSpan ReadTicketTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PublishTicketCeiling = TimeSpan.FromHours(12);

    private readonly IDistributedCache _cache;

    public ScreenTicketStore(IDistributedCache cache) => _cache = cache;

    public async Task<(string Ticket, DateTime ExpiresUtc)> IssueReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string ticket = NewTicket();
        await _cache.SetStringAsync(ReadKey(path, ticket), path, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ReadTicketTtl }, cancellationToken);
        return (ticket, DateTime.UtcNow.Add(ReadTicketTtl));
    }

    public async Task<string> IssuePublishAsync(string path, Guid sessionId, CancellationToken cancellationToken = default)
    {
        string ticket = NewTicket();
        await _cache.SetStringAsync(PublishKey(path, ticket), sessionId.ToString("N"), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = PublishTicketCeiling }, cancellationToken);
        return ticket;
    }

    public Task RevokePublishAsync(string path, string ticket, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(PublishKey(path, ticket), cancellationToken);

    /// <summary> Anahtar yol + bileti birlikte taşır: başka yolun bileti bu yolda hiç bulunmaz. </summary>
    public async Task<bool> ValidateReadAsync(string path, string ticket, CancellationToken cancellationToken = default)
        => await _cache.GetStringAsync(ReadKey(path, ticket), cancellationToken) != null;

    public async Task<bool> ValidatePublishAsync(string path, string ticket, CancellationToken cancellationToken = default)
        => await _cache.GetStringAsync(PublishKey(path, ticket), cancellationToken) != null;

    private static string NewTicket() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string ReadKey(string path, string ticket) => $"remotedesk_read_{path}_{ticket}";

    private static string PublishKey(string path, string ticket) => $"remotedesk_publish_{path}_{ticket}";
}
