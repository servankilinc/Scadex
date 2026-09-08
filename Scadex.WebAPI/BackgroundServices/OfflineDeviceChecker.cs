using Scadex.Business.Abstract;

namespace Scadex.WebAPI.BackgroundServices;

public class OfflineDeviceChecker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfflineDeviceChecker> _logger;
    private readonly TimeSpan _staleAfter;
    private readonly TimeSpan _interval;

    private const int DefaultStaleAfterSeconds = 86400; // 1 gün içerisinde 
    private const int DefaultSweepIntervalSeconds = 300; // 5dk

    public OfflineDeviceChecker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OfflineDeviceChecker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        int staleAfterSeconds = configuration.GetValue("Scada:StaleAfterSeconds", DefaultStaleAfterSeconds);
        int intervalSeconds = configuration.GetValue("Scada:SweepIntervalSeconds", DefaultSweepIntervalSeconds);

        // Eşik değeri periyottan kısa olursa cihazlar iki tarama arasinda Offline/Online arasinda gidip gelir. periyot*2 daha büyükse onla devam edilir
        _interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds)); //  en az 5 sn olmalı 
        _staleAfter = TimeSpan.FromSeconds(Math.Max(intervalSeconds * 2, staleAfterSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OfflineDeviceChecker basladi: her {Interval} sn, esik {Stale} sn", _interval.TotalSeconds, _staleAfter.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var channelEventService = scope.ServiceProvider.GetRequiredService<IChannelEventService>();

                int swept = await channelEventService.SetOfflineDevicesAsync(_staleAfter, stoppingToken);
                if (swept > 0)
                    _logger.LogInformation("{Count} cihaz Offline'a cekildi", swept);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "OfflineDeviceChecker turu basarisiz");
            }
        }
    }
}
