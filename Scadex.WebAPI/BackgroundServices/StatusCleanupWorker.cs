using Scadex.Business.Abstract;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary>
/// Monitoringi kaplı cihaz ve kameraların 23 saatten içinde haberleşilmediyse durumları "bilinmiyor"a çeker ve tüm otomasyonları uzlaştırır.
/// Monitoringi açık cihaz ve kameraları <c>MonitoredAssetProbeWorker</c> yoklar; burası offline/online bilgisini set etmez
/// </summary>
public class StatusCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatusCleanupWorker> _logger;
    private readonly TimeSpan _interval;

    private const int DefaultSweepIntervalSeconds = 300; // 5dk

    public StatusCleanupWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<StatusCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        int intervalSeconds = configuration.GetValue("Scada:SweepIntervalSeconds", DefaultSweepIntervalSeconds);
        _interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds)); //  en az 5 sn olmalı
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StatusCleanupWorker basladi: her {Interval} sn", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var automationStatusService = scope.ServiceProvider.GetRequiredService<IAutomationStatusService>();

                var result = await automationStatusService.SweepAsync(stoppingToken);
                if (result.ClearedDevices > 0 || result.ReconciledAutomations > 0)
                    _logger.LogInformation("Otomasyon durum taramasi: {Devices} cihazin eskiyen durumu temizlendi, {Automations} otomasyon uzlastirildi",
                        result.ClearedDevices, result.ReconciledAutomations);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "StatusCleanupWorker turu basarisiz");
            }
        }
    }
}
