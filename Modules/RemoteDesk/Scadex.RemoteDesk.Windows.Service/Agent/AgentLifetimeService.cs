using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Service.Services.Streaming;
using Scadex.RemoteDesk.Windows.Service.Supervisor;

namespace Scadex.RemoteDesk.Windows.Service.Agent;

/// <summary>
/// Agent'ın açılışı ve kapanışı. Açılışta servisin durdurma olayını (<see cref="AgentLauncher.StopSignalName"/>) bekler: servis Agent'ı
/// durdurmak istediğinde (oturum değişti, servis duruyor) olay işaretlenir ve host düzgün kapanır. Kapanışta yayınlar önce nazikçe durur
/// (FFmpeg'e 'q'); yetişmezse Job Object süreç bitince yine öldürür. Hosted service'ler ters sırada durduğu için EN SON kaydedilir.
/// </summary>
public sealed class AgentLifetimeService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IScreenStreamService _streams;
    private readonly SessionContext _session;
    private readonly ILogger<AgentLifetimeService> _logger;
    private RegisteredWaitHandle? _wait;
    private EventWaitHandle? _stopSignal;

    public AgentLifetimeService(IHostApplicationLifetime lifetime, IScreenStreamService streams, SessionContext session, ILogger<AgentLifetimeService> logger)
    {
        _lifetime = lifetime;
        _streams = streams;
        _session = session;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent başladı: sürüm {Version}, oturum {SessionId} ({User}), kimlik {Identity}, günlük {Folder}",
            AgentStateService.AgentVersion, _session.SessionId, _session.UserName, _session.IsSystem ? "SYSTEM" : "kullanıcı (geliştirme)",
            Shell.AgentLog.Folder);

        if (EventWaitHandle.TryOpenExisting(AgentLauncher.StopSignalName(Environment.ProcessId), out var signal))
        {
            _stopSignal = signal;
            _wait = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
            {
                _logger.LogInformation("Servis durdurma istedi");
                _lifetime.StopApplication();
            }, null, Timeout.Infinite, executeOnlyOnce: true);
        }
        else if (_session.IsSystem)
        {
            _logger.LogWarning("Servisin durdurma olayı bulunamadı; Agent servis dışında başlatılmış olabilir");
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent kapanıyor: yayınlar durduruluyor");
        try { await _streams.StopAllAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "Yayınlar zamanında durmadı"); }
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _stopSignal?.Dispose();
    }
}
