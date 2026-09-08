using Scadex.Business.Abstract;
using Scadex.Business.Utils.ClipCaptureQueue;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary> Klip çekimlerini sırayla yapar. <b>Sıralı çalışır</b>, paralel degil  </summary>
public class ClipCaptureWorker : BackgroundService
{
    private readonly IClipCaptureQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClipCaptureWorker> _logger;

    public ClipCaptureWorker(IClipCaptureQueue queue, IServiceScopeFactory scopeFactory, ILogger<ClipCaptureWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClipCaptureWorker basladi");

        // queue channel kuyruk da değişiklik olana kadar bekler sistemi yormaz pooling yapmaz yani event driven çalışır 
        await foreach (long captureId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();

                await cameraService.RunClipCaptureAsync(captureId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"ClipCaptureWorker, Klip çekimi {captureId} yürütülürken hata oluştur");
            }
        }
    }
}
