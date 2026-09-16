using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Engine;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Queue;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scadex.Signalization.BackgroundServices;

/// <summary> Kabin bazinda SIRALI işlenir, kabinler arasinda PARALEL çalışır, Her kabin için ayrı bir kanal oluşur key olarak cabinetId kullanılır </summary>
public sealed class SignalEventWorker : BackgroundService
{
    private readonly SignalEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalEventWorker> _logger;

    /// <summary> Her CabinetId için ayrı bir Channel oluşturuluyor. </summary>
    private readonly ConcurrentDictionary<Guid, Channel<SignalWorkItem>> _lanes = new();

    /// <summary> Her kabinetin çalışan worker task'ını tutuyor. </summary>
    private readonly ConcurrentDictionary<Guid, Task> _laneTasks = new();

    public SignalEventWorker(SignalEventQueue queue, IServiceScopeFactory scopeFactory, ILogger<SignalEventWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalEventWorker basladi");

        try
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken))
            {
                // kabinin kanalını bul veya oluştur
                var lane = _lanes.GetOrAdd(
                    key: item.CabinetId,
                    valueFactory: (cabinetId) =>
                    {
                        var channel = Channel.CreateUnbounded<SignalWorkItem>(
                            new UnboundedChannelOptions
                            {
                                SingleReader = true,
                                SingleWriter = true
                            }
                        );
                        _laneTasks[cabinetId] = RunLaneAsync(cabinetId, channel.Reader, stoppingToken);
                        return channel;
                    }
                );

                lane.Writer.TryWrite(item);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        foreach (var lane in _lanes.Values)
            lane.Writer.TryComplete();

        var pending = _laneTasks.Values.ToArray();
        if (pending.Length > 0)
            await Task.WhenAll(pending);
    }

    private async Task RunLaneAsync(Guid cabinetId, ChannelReader<SignalWorkItem> reader, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<OperatorSessionEngine>();
                    await engine.HandleAsync(item, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Kabin {CabinetId}: sinyalizasyon olayi kapanis nedeniyle yarida kesildi ({Item})", cabinetId, item.GetType().Name);
                    return;
                }
                catch (Exception exception)
                {
                    // Tek bir olayın hatası diziyi durdurmamalı: sonraki olaylar işlenmeye devam eder.
                    _logger.LogError(exception, "Kabin {CabinetId}: sinyalizasyon olayi islenemedi ({Item})", cabinetId, item.GetType().Name);
                }
                finally
                {
                    if (item is TimerWork timer)
                        _queue.CompleteTimer(timer);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
