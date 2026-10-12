using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Scadex.Business.Utils.MonitoredAssetService;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Monitoring.Queries;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary> <see cref="Scadex.Model.Entities.Abstract.IMonitoredAsset"/> uygulayan entityleri yoklar: izleme portu varsa TCP connect, yoksa ICMP ping. </summary>
public class MonitoredAssetProbeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoredAssetProbeWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _probeTimeout;

    /// <summary>Entity Id -> Son yoklama tarihi.</summary>
    private readonly Dictionary<Guid, DateTime> _lastProbedAt = [];

    private const int DefaultSweepIntervalSeconds = 30;
    private const int DefaultProbeTimeoutMs = 3000;

    public MonitoredAssetProbeWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MonitoredAssetProbeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        int intervalSeconds = configuration.GetValue("Monitoring:SweepIntervalSeconds", DefaultSweepIntervalSeconds);
        int timeoutMs = configuration.GetValue("Monitoring:ProbeTimeoutMs", DefaultProbeTimeoutMs);

        _interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds));
        _probeTimeout = TimeSpan.FromMilliseconds(Math.Max(250, timeoutMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonitoredAssetProbeWorker basladi: her {Interval} sn, sonda zaman asimi {Timeout} ms", _interval.TotalSeconds, _probeTimeout.TotalMilliseconds);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "MonitoredAssetProbeWorker turu basarisiz");
            }
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sources = scope.ServiceProvider.GetServices<IMonitoredAssetProbeSource>();

        var now = DateTime.UtcNow;
        var alive = new HashSet<Guid>();

        foreach (var source in sources)
        {
            var targets = await source.GetProbeTargetsAsync(stoppingToken);

            foreach (var target in targets)
            {
                alive.Add(target.Id);
                if (!IsDue(target, now)) continue;

                // Port varsa TCP connect (hizmet ayakta mi), yoksa ICMP ping (makine agda mi)
                var result = target.MonitoringPort is { } port
                    ? await ProbeTcpAsync(target.IpAddress, port, stoppingToken)
                    : await ProbeIcmpAsync(target.IpAddress, stoppingToken);
                _lastProbedAt[target.Id] = DateTime.UtcNow;

                var writeResult = await source.RecordProbeResultAsync(target.Id, result, stoppingToken);
                if (!writeResult.IsSuccess)
                {
                    _logger.LogWarning("{Type} {Name} ({Id}) yoklama sonucu yazilamadi: {Message}", source.AssetTypeName, target.Name, target.Id, writeResult.Message);
                }
            }
        }

        // Silinmis / izlemesi kapatilmis varliklarin kaydi bellekte birikmesin.
        foreach (var id in _lastProbedAt.Keys.Where(id => !alive.Contains(id)).ToList())
            _lastProbedAt.Remove(id);
    }

    private bool IsDue(MonitoredAssetProbeTargetDto target, DateTime nowUtc)
    {
        if (!_lastProbedAt.TryGetValue(target.Id, out var last)) return true;

        // Periyot varlik basina; en az bir tur beklenir ki yanlis yapilandirilmis
        // (orn. 0) bir deger sondayi surekli calistirmasin.
        var period = TimeSpan.FromSeconds(Math.Max(5, target.PingIntervalSec));
        return nowUtc - last >= period;
    }

    /// <summary> Izleme portu bos varlik icin ICMP echo </summary>
    private async Task<MonitoredAssetProbeResultDto> ProbeIcmpAsync(string ipAddress, CancellationToken stoppingToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, _probeTimeout, cancellationToken: stoppingToken);

            return reply.Status == IPStatus.Success
                ? new MonitoredAssetProbeResultDto { Reachable = true, RttMs = (int)reply.RoundtripTime }
                : Unreachable($"Ping basarisiz ({reply.Status}): {ipAddress}");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PingException exception)
        {
            return Unreachable($"Ping basarisiz: {exception.InnerException?.Message ?? exception.Message} ({ipAddress})");
        }
        catch (Exception exception)
        {
            return Unreachable($"Ping basarisiz: {exception.Message}");
        }
    }

    private async Task<MonitoredAssetProbeResultDto> ProbeTcpAsync(string ipAddress, int port, CancellationToken stoppingToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutSource.CancelAfter(_probeTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port, timeoutSource.Token);
            stopwatch.Stop();

            return new MonitoredAssetProbeResultDto
            {
                Reachable = true,
                RttMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Unreachable($"Zaman asimi ({_probeTimeout.TotalMilliseconds:0} ms): {ipAddress}:{port}");
        }
        catch (SocketException exception)
        {
            return Unreachable($"Baglanti hatasi ({exception.SocketErrorCode}): {ipAddress}:{port}");
        }
        catch (Exception exception)
        {
            return Unreachable($"Yoklama basarisiz: {exception.Message}");
        }
    }

    private static MonitoredAssetProbeResultDto Unreachable(string error) => new() { Reachable = false, Error = error };
}
