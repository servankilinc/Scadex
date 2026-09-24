using Scadex.Business.Abstract;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary>
/// Monitoringi kaplı cihaz ve kameraların 23 saatten içinde haberleşilmediyse durumları "bilinmiyor"a çeker ve tüm kabinleri uzlaştırır.
/// Monitoringi açık cihaz ve kameraları <c>MonitoredAssetProbeWorker</c> yoklar; burası offline/online bilgisini set etmez
/// </summary>
public class OfflineDeviceChecker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfflineDeviceChecker> _logger;
    private readonly TimeSpan _interval;

    private const int DefaultSweepIntervalSeconds = 300; // 5dk

    public OfflineDeviceChecker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OfflineDeviceChecker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        int intervalSeconds = configuration.GetValue("Scada:SweepIntervalSeconds", DefaultSweepIntervalSeconds);
        _interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds)); //  en az 5 sn olmalı
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OfflineDeviceChecker basladi: her {Interval} sn", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cabinetStatusService = scope.ServiceProvider.GetRequiredService<ICabinetStatusService>();

                var result = await cabinetStatusService.SweepAsync(stoppingToken);
                if (result.ClearedDevices > 0 || result.ClearedCameras > 0 || result.ReconciledCabinets > 0)
                    _logger.LogInformation("Kabin durum taramasi: {Devices} cihazin ve {Cameras} kameranin eskiyen durumu temizlendi, {Cabinets} kabin uzlastirildi",
                        result.ClearedDevices, result.ClearedCameras, result.ReconciledCabinets);
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
