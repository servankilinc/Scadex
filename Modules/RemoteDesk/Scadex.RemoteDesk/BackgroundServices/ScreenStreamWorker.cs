using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.RemoteDesk.Streaming;

namespace Scadex.RemoteDesk.BackgroundServices;

/// <summary>
/// Açılışta önceki süreçten açık kalan yayın oturumlarını kapatır, sonra <see cref="Interval"/>'da bir koordinatörün süpürmesini çalıştırır
/// (düşen kiralamalar, izleyicisiz yayınlar, durdurmaya uymayan yayıncılar). Yayın yokken tur boştur (DB/MediaMTX'e gidilmez).
/// </summary>
public sealed class ScreenStreamWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly ScreenStreamCoordinator _coordinator;
    private readonly ScreenSessionStore _store;
    private readonly RemoteDeskOptions _options;
    private readonly ILogger<ScreenStreamWorker> _logger;

    public ScreenStreamWorker(ScreenStreamCoordinator coordinator, ScreenSessionStore store, IOptions<RemoteDeskOptions> options, ILogger<ScreenStreamWorker> logger)
    {
        _coordinator = coordinator;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sık kurulum hatası: yayın adresi yerel. Uzaktaki PC için 127.0.0.1 kendisidir; FFmpeg MediaMTX'e ulaşamaz, yayın hiç başlamaz.
        if (Uri.TryCreate(_options.PublishRtspBaseUrl, UriKind.Absolute, out var publishUri)
            && (publishUri.IsLoopback || publishUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            _logger.LogWarning("RemoteDesk: Modules:RemoteDesk:PublishRtspBaseUrl yerel adres ({Url}) — yalnızca bu sunucudaki PC yayın yapabilir. " +
                               "Sahadaki PC'ler için sunucunun ağ adresi verilmeli.", _options.PublishRtspBaseUrl);

        try
        {
            int orphans = await _store.CloseOrphansAsync(stoppingToken);
            if (orphans > 0)
                _logger.LogInformation("RemoteDesk: önceki çalışmadan açık kalan {Count} yayın oturumu kapatıldı", orphans);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "RemoteDesk: açık kalan yayın oturumları kapatılamadı");
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _coordinator.SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RemoteDesk: yayın süpürme turu başarısız");
            }
        }
    }
}
