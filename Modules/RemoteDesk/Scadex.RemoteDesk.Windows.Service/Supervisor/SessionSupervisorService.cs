using System.ServiceProcess;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Supervisor;

/// <summary>
/// Servisin tek işi: doğru kullanıcı oturumunda TEK bir Agent çalıştırmak. Merkezle konuşmaz.
/// <list type="bullet">
/// <item>Hedef oturum <see cref="Sessions.SelectTarget"/>'tır (aktif + kullanıcılı; önce konsol). Hedef yoksa Agent yoktur.</item>
/// <item>Hedef değişince (hızlı kullanıcı değiştirme, konsol ↔ RDP) ya da Agent'ın oturumunda oturum açılıp kapanınca eski Agent nazikçe
/// durdurulur, yenisi başlar. Aynı oturumda RDP bağlan/kes ve kilit Agent'ı yeniden başlatmaz — monitör listesini Agent kendisi izler.</item>
/// <item>Agent beklenmedik biterse geri çekilerek yeniden başlatılır (1/2/5/10/30 sn; 60 sn yaşadıysa sıfırlanır).</item>
/// <item>Olaylar kaçarsa diye 15 sn'de bir uzlaştırma (ucuz WTS sorgusu) yapılır.</item>
/// </list>
/// </summary>
public sealed class SessionSupervisorService : BackgroundService
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];
    private static readonly TimeSpan StableRun = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Reconcile = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(8);

    private readonly AgentLauncher _launcher;
    private readonly SessionChangeQueue _changes;
    private readonly ILogger<SessionSupervisorService> _logger;

    private AgentProcess? _agent;
    private int _failures;
    private DateTime _nextStartUtc = DateTime.MinValue;

    public SessionSupervisorService(AgentLauncher launcher, SessionChangeQueue changes, ILogger<SessionSupervisorService> logger)
    {
        _launcher = launcher;
        _changes = changes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servis başladı: sürüm {Version}", typeof(SessionSupervisorService).Assembly.GetName().Version);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool restart = DrainChanges();
                await ReconcileAsync(restart);
                await WaitForWakeAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (_agent is not null)
            {
                _logger.LogInformation("Servis duruyor: Agent durduruluyor (süreç {ProcessId})", _agent.ProcessId);
                await StopAgentAsync();
            }
        }
    }

    /// <summary> Bekleyen oturum olaylarını okur; Agent'ın oturumunda oturum açıldı/kapandıysa yeniden başlatma ister. </summary>
    private bool DrainChanges()
    {
        bool restart = false;
        while (_changes.Reader.TryRead(out var change))
        {
            _logger.LogInformation("Oturum olayı: {Reason}, oturum {SessionId}", change.Reason, change.SessionId);
            if (_agent is not null && change.SessionId == _agent.SessionId
                && change.Reason is SessionChangeReason.SessionLogon or SessionChangeReason.SessionLogoff)
                restart = true;
        }
        return restart;
    }

    private async Task ReconcileAsync(bool restart)
    {
        int? target;
        try { target = Sessions.SelectTarget(Sessions.Enumerate()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oturumlar okunamadı");
            return;
        }

        // Biten Agent: beklenmedik çıkışsa geri çekilme uygulanır.
        if (_agent is { Exited.IsCompleted: true } ended)
        {
            uint code = ended.Exited.Result;
            bool stable = DateTime.UtcNow - ended.StartedUtc >= StableRun;
            if (stable) _failures = 0;
            var delay = Backoff[Math.Min(_failures, Backoff.Length - 1)];
            _failures++;
            _nextStartUtc = DateTime.UtcNow + delay;
            _logger.Log(stable ? LogLevel.Warning : LogLevel.Error,
                "Agent beklenmedik şekilde bitti (süreç {ProcessId}, çıkış kodu {ExitCode}); {Delay} sn sonra yeniden başlatılacak",
                ended.ProcessId, code, delay.TotalSeconds);
            ended.Dispose();
            _agent = null;
        }

        if (_agent is not null && (restart || _agent.SessionId != target))
        {
            _logger.LogInformation("Agent oturumu değişiyor: {From} → {To} ({Reason})", _agent.SessionId,
                target?.ToString() ?? "yok", restart ? "oturum açıldı/kapandı" : "hedef oturum değişti");
            await StopAgentAsync();
            _failures = 0;
            _nextStartUtc = DateTime.MinValue;
        }

        if (_agent is null && target is { } session && DateTime.UtcNow >= _nextStartUtc)
        {
            try
            {
                _agent = _launcher.Start(session);
            }
            catch (Exception ex)
            {
                var delay = Backoff[Math.Min(_failures, Backoff.Length - 1)];
                _failures++;
                _nextStartUtc = DateTime.UtcNow + delay;
                _logger.LogError(ex, "Agent oturum {SessionId}'de başlatılamadı; {Delay} sn sonra yeniden denenecek", session, delay.TotalSeconds);
            }
        }
    }

    /// <summary> Oturum olayı, Agent'ın çıkışı, bekleyen yeniden başlatma zamanı ya da uzlaştırma süresi — hangisi önce gelirse. </summary>
    private async Task WaitForWakeAsync(CancellationToken stoppingToken)
    {
        var wait = Reconcile;
        if (_agent is null && _nextStartUtc > DateTime.UtcNow && _nextStartUtc - DateTime.UtcNow < wait)
            wait = _nextStartUtc - DateTime.UtcNow;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(wait);
        var waits = new List<Task> { _changes.Reader.WaitToReadAsync(timeout.Token).AsTask() };
        if (_agent is not null) waits.Add(_agent.Exited);

        try { await Task.WhenAny(waits); }
        catch (OperationCanceledException) { }
        stoppingToken.ThrowIfCancellationRequested();
    }

    private async Task StopAgentAsync()
    {
        if (_agent is null) return;
        var agent = _agent;
        _agent = null;
        try { await agent.StopAsync(StopGrace); }
        catch (Exception ex) { _logger.LogWarning(ex, "Agent durdurulurken hata (süreç {ProcessId})", agent.ProcessId); }
        _logger.LogInformation("Agent durdu (süreç {ProcessId})", agent.ProcessId);
        agent.Dispose();
    }
}
