using Scadex.Business.Utils.MediaGateway;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary>
/// MediaMTX'te birikmis pathlerini temizler.
/// <param/>
/// Kamera bilgilerini güncellemediğimiz sürece Yollar kendiliginden silinmez: 
/// Hiç kullanılmayan bir kameranin yolu izleyicisi olmasa da sonsuza kadar kalıyor du.
/// </summary>
public class MediaPathCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaPathCleanupWorker> _logger;
    private readonly int _runAtHour;

    private const int DefaultRunAtHour = 3;

    public MediaPathCleanupWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MediaPathCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _runAtHour = Math.Clamp(configuration.GetValue("Jobs:MediaPathCleanupHour", DefaultRunAtHour), 0, 23);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MediaPathCleanupWorker basladi: her gun saat {Hour}:00", _runAtHour);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTime.Now);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "MediaPathCleanupWorker turu basarisiz");
            }
        }
    }

    private TimeSpan DelayUntilNextRun(DateTime now)
    {
        var next = now.Date.AddHours(_runAtHour);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    private async Task CleanupAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediaGateway = scope.ServiceProvider.GetRequiredService<IMediaGateway>();

        var listResult = await mediaGateway.ListPathsAsync(stoppingToken);
        if (!listResult.IsSuccess || listResult.Data is null)
        {
            _logger.LogWarning("Yol temizligi atlandi, liste alinamadi: {Message}", listResult.Message);
            return;
        }

        int deleted = 0, protectedCount = 0;

        foreach (var path in listResult.Data)
        {
            if (!ShouldDelete(path, out string protectReason))
            {
                if (protectReason.Length > 0)
                {
                    protectedCount++;
                    _logger.LogInformation("Yol korundu ({Reason}): {Path}", protectReason, path.Name);
                }
                continue;
            }

            var deleteResult = await mediaGateway.DeletePathAsync(path.Name, stoppingToken);
            if (deleteResult.IsSuccess)
            {
                deleted++;
                _logger.LogInformation("Yol silindi: {Path}", path.Name);
            }
            else
            {
                _logger.LogWarning("Yol silinemedi: {Path} — {Message}", path.Name, deleteResult.Message);
            }
        }

        _logger.LogInformation("Yol temizligi bitti: {Deleted} silindi, {Protected} korundu", deleted, protectedCount);
    }

    private static bool ShouldDelete(MediaPathInfo path, out string protectReason)
    {
        protectReason = string.Empty;

        // 1) Bizim uretmedigimiz pathlere hiç dokunulmaz.
        if (!IMediaGateway.IsManagedPathName(path.Name)) return false;

        // 2) Yapilandirmada yoksa silinecek bir sey de yok.
        if (!path.IsConfigured) return false;

        // 3) KAYIT YAPAN path asla silinmez.
        if (path.RecordEnabled)
        {
            // Cekim akisi yolu kendi `finally` blogunda dusuruyor. Buraya dusen bir
            // klip yolu ya hala cekiliyordur ya da cekimi cokmus bir artiktir; ikisi
            // ayirt edilemedigi icin KORUNUR ve gorunur olsun diye loglanir.
            protectReason = IMediaGateway.IsClipPathName(path.Name)
                ? "kayit yapan klip yolu"
                : "kayit acik";
            return false;
        }

        // 4) Izleyicisi olan yol silinmez — birinin ekranindaki yayin kesilirdi.
        if (path.ReaderCount > 0)
        {
            protectReason = $"{path.ReaderCount} aktif izleyici";
            return false;
        }

        return true;
    }
}
