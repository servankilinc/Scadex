using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Runtime;
using System.Collections.Concurrent;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.BackgroundServices;

public sealed class EntrySnapshotWorker : BackgroundService
{
    private readonly EntrySnapshotQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EntrySnapshotWorker> _logger;
    private readonly ConcurrentDictionary<Task, byte> _running = new();

    public EntrySnapshotWorker(EntrySnapshotQueue queue, IServiceScopeFactory scopeFactory, ILogger<EntrySnapshotWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EntrySnapshotWorker basladi");

        try
        {
            // kuyruğa eleman düşünce çalışır
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                var task = RunSeriesAsync(job, stoppingToken);
                if (task.IsCompleted)
                    continue;

                _running.TryAdd(task, 0);
                _ = task.ContinueWith(finished => _running.TryRemove(finished, out _), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        var pending = _running.Keys.ToArray();
        if (pending.Length > 0)
            await Task.WhenAll(pending);
    }

    private async Task RunSeriesAsync(EntrySnapshotWorkItem job, CancellationToken stoppingToken)
    {
        try
        {
            for (int sequence = 1; sequence <= job.Count; sequence++)
            {
                var dueAt = job.StartAtUtc.AddMilliseconds((sequence - 1) * (double)job.IntervalMs);
                var wait = dueAt - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, stoppingToken);

                await CaptureOneAsync(job, sequence, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Oturum {SessionId}: kare serisi kapanis nedeniyle yarida kesildi", job.SessionId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Oturum {SessionId}: kare serisi yurutulurken hata olustu", job.SessionId);
        }
    }

    private async Task CaptureOneAsync(EntrySnapshotWorkItem job, int sequence, CancellationToken stoppingToken)
    {
        // 1) Her görüntü kendi scope'unda: DbContext'ler paylasilamaz ve seriler paralel calisir.
        using var scope = _scopeFactory.CreateScope();
        var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();
        var db = scope.ServiceProvider.GetRequiredService<SignalizationDbContext>();

        // 2) Scadex çekirdeğine kamera kaydı aldırılır
        var now = DateTime.UtcNow;
        var captureResult = await cameraService.CreateCaptureAsync(
            cameraId: job.CameraId,
            request: new CameraCaptureCreateDto
            {
                Type = CaptureType.Snapshot
            },
            cancellationToken: stoppingToken
        );

        // 3) operatör işlem hareket nesnesi açılır
        var sessionEvent = new OperatorSessionEvent
        {
            SessionId = job.SessionId,
            OccurredAtUtc = now,
            ReceivedAtUtc = now,
            InnerDoorId = job.InnerDoorId
        };

        // 4) Scadex çekirdeği kamera kaydı alabildiyse operatör oturumuyla ilişkilendirilir değilse Hareket kaydı başarısız işaretlenir
        if (!captureResult.IsSuccess)
        {
            sessionEvent.Type = SessionEventType.SnapshotFailed;
            sessionEvent.Detail = $"{sequence}: {captureResult.Error.Description}";
        }
        else
        {
            db.OperatorSessionCaptures.Add(
                new OperatorSessionCapture
                {
                    SessionId = job.SessionId,
                    CameraCaptureId = captureResult.Data.Id,
                    Sequence = sequence
                }
            );
            sessionEvent.CameraCaptureId = captureResult.Data.Id;
            sessionEvent.OccurredAtUtc = captureResult.Data.CapturedAtUtc;

            if (captureResult.Data.Status == CaptureStatus.Available)
            {
                sessionEvent.Type = SessionEventType.SnapshotTaken;
                sessionEvent.Detail = sequence.ToString();
            }
            else
            {
                sessionEvent.Type = SessionEventType.SnapshotFailed;
                sessionEvent.Detail = $"{sequence}: {captureResult.Data.FailureReason}";
            }
        }

        if (sessionEvent.Detail?.Length > 512)
            sessionEvent.Detail = sessionEvent.Detail[..512];

        db.OperatorSessionEvents.Add(sessionEvent);
        await db.SaveChangesAsync(stoppingToken);
    }
}
