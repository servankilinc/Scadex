using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Engine;

namespace Scadex.Signalization.Runtime;

/// <summary>
/// Motorun yurutucusu: <b>kabin bazinda SIRALI, kabinler arasinda PARALEL</b>.
/// <list type="bullet">
/// <item>Ayni kabinin olaylari sirayla islenir — durum makinesinde yaris olmaz ve kabin ortak sireni tek bir
/// uzlastirmadan gecer.</item>
/// <item>Bir kabinde SCADA'nin 180 sn'lik komut zaman asimi diger kabinleri bloke etmez: her kabinin kendi seridi
/// (kanal + tuketici gorevi) vardir. Bosta bekleyen serit thread tutmaz.</item>
/// </list>
/// Kuyruk ZORUNLUDUR: <c>SendAsync</c> SCADA yanit verene kadar bekler; kart isteginin icinde calissaydi SCADA karti
/// bizim yanitimizi beklerken ayni kartin web sunucusunu cagirmis olurduk.
/// </summary>
public sealed class SignalEventWorker : BackgroundService
{
    private readonly SignalEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalEventWorker> _logger;

    private readonly ConcurrentDictionary<Guid, Channel<SignalWorkItem>> _lanes = new();
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
                var lane = _lanes.GetOrAdd(item.CabinetId, cabinetId =>
                {
                    var channel = Channel.CreateUnbounded<SignalWorkItem>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                    _laneTasks[cabinetId] = RunLaneAsync(cabinetId, channel.Reader, stoppingToken);
                    return channel;
                });

                lane.Writer.TryWrite(item);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Kapaniyoruz; seritler asagida kapatilip beklenir.
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
                    // Tek bir olayin hatasi seridi durdurmamali: sonraki olaylar (orn. dis kapi kapanisi) islenmeye devam eder.
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
