using System.Collections.Concurrent;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.ClipCaptureQueue;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary>  Klip çekimlerini yürütür. Parallel çalışır. </summary>
public class ClipCaptureWorker : BackgroundService
{
    private readonly IClipCaptureQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClipCaptureWorker> _logger;

    /// <summary>Devam eden çekimler — kapanışta beklenebilsin diye tutulur.</summary>
    private readonly ConcurrentDictionary<Task, byte> _running = new();

    public ClipCaptureWorker(IClipCaptureQueue queue, IServiceScopeFactory scopeFactory, ILogger<ClipCaptureWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClipCaptureWorker basladi");

        try
        {
            // Kuyruk (channel) değişiklik olana kadar bekler; polling yapmaz, event driven çalışır.
            // Okuyucu TEK, ama okunan iş beklenmeden başlatılır — bu sayede paralellik sağlanır.
            await foreach (long captureId in _queue.ReadAllAsync(stoppingToken))
            {
                var task = RunCaptureAsync(captureId, stoppingToken);

                if (task.IsCompleted) continue;

                _running.TryAdd(task, 0);
                _ = task.ContinueWith(finished => _running.TryRemove(finished, out _), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Kapaniyoruz; devam eden cekimler asagida beklenir.
        }

        // Yarim kalan cekim, dusurulmemis bir MediaMTX yolu ve silinmemis bir gecici
        // klasor birakir. Kapanmadan once bitmelerine firsat verilir.
        var pending = _running.Keys.ToArray();
        if (pending.Length > 0)
        {
            _logger.LogInformation("ClipCaptureWorker kapaniyor: {Count} devam eden cekim bekleniyor", pending.Length);
            await Task.WhenAll(pending);
        }
    }

    private async Task RunCaptureAsync(long captureId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();

            await cameraService.RunClipCaptureAsync(captureId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Klip cekimi {CaptureId} kapanis nedeniyle yarida kesildi", captureId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "ClipCaptureWorker, Klip cekimi {CaptureId} yurutulurken hata olustu", captureId);
        }
    }
}
