using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Signalization.Data;
using Scadex.Signalization.Entities;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Runtime;

/// <summary>
/// Dis kapi acilisinda kare serisini ceker. Kareler cekirdegin mevcut yolundan gecer
/// (<c>ICameraService.CreateCaptureAsync</c>, <c>Snapshot</c>) — bu yol snapshot ONBELLEGINI ATLAR, yani 1 sn arayla
/// cekilen kareler gercekten farklidir. Dosya ve <c>CameraCapture</c> satiri cekirdegindir; modul yalnizca bagi yazar.
/// <para>Zamanlama BASLANGICTAN BASLANGICA'dir (<c>T0 + i × aralik</c>): "cek, 1 sn uyu" olsaydi kameranin yanit
/// suresi her kareye eklenir ve seri kayardi. Seriler birbirini beklemez (paralel).</para>
/// </summary>
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
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                var task = RunSeriesAsync(job, stoppingToken);
                if (task.IsCompleted) continue;

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

    private async Task RunSeriesAsync(EntrySnapshotJob job, CancellationToken stoppingToken)
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

    private async Task CaptureOneAsync(EntrySnapshotJob job, int sequence, CancellationToken stoppingToken)
    {
        // Her kare kendi scope'unda: DbContext'ler paylasilamaz ve seriler paralel calisir.
        using var scope = _scopeFactory.CreateScope();
        var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();
        var db = scope.ServiceProvider.GetRequiredService<SignalizationDbContext>();

        var now = DateTime.UtcNow;
        var result = await cameraService.CreateCaptureAsync(job.CameraId, new CameraCaptureCreateDto { Type = CaptureType.Snapshot }, stoppingToken);

        var sessionEvent = new OperatorSessionEvent
        {
            SessionId = job.SessionId,
            OccurredAtUtc = now,
            ReceivedAtUtc = now
        };

        if (!result.IsSuccess)
        {
            // Kamera bulunamadi / pasif: cekim satiri hic dogmadi.
            sessionEvent.Type = SessionEventType.SnapshotFailed;
            sessionEvent.Detail = $"{sequence}: {result.Error.Description}";
        }
        else
        {
            // Kameraya ulasilamasa da cekirdek Failed durumlu bir cekim satiri yazar; bag her durumda kurulur.
            db.OperatorSessionCaptures.Add(new OperatorSessionCapture { SessionId = job.SessionId, CameraCaptureId = result.Data.Id, Sequence = sequence });
            sessionEvent.CameraCaptureId = result.Data.Id;
            sessionEvent.OccurredAtUtc = result.Data.CapturedAtUtc;

            if (result.Data.Status == CaptureStatus.Available)
            {
                sessionEvent.Type = SessionEventType.SnapshotTaken;
                sessionEvent.Detail = sequence.ToString();
            }
            else
            {
                sessionEvent.Type = SessionEventType.SnapshotFailed;
                sessionEvent.Detail = $"{sequence}: {result.Data.FailureReason}";
            }
        }

        if (sessionEvent.Detail?.Length > 512)
            sessionEvent.Detail = sessionEvent.Detail[..512];

        db.OperatorSessionEvents.Add(sessionEvent);
        await db.SaveChangesAsync(stoppingToken);
    }
}
