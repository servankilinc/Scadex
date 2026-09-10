using Scadex.Business.Abstract;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary> Saklama suresi dolmus cekim DOSYALARINI gunluk olarak siler. </summary>
public class CaptureRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CaptureRetentionWorker> _logger;
    private readonly int _runAtHour;

    private const int DefaultRunAtHour = 4;

    public CaptureRetentionWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<CaptureRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _runAtHour = Math.Clamp(configuration.GetValue("Cameras:RetentionSweepHour", DefaultRunAtHour), 0, 23);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CaptureRetentionWorker basladi: her gun saat {Hour}:00", _runAtHour);

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
                using var scope = _scopeFactory.CreateScope();
                var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();

                int purged = await cameraService.PurgeExpiredCaptureFilesAsync(stoppingToken);
                if (purged > 0)
                    _logger.LogInformation("{Count} cekim dosyasi saklama suresi dolduğu icin silindi", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "CaptureRetentionWorker turu basarisiz");
            }
        }
    }

    private TimeSpan DelayUntilNextRun(DateTime now)
    {
        var next = now.Date.AddHours(_runAtHour);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
