using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Data;

namespace Scadex.Signalization.Runtime;

/// <summary>
/// Oturumlarin zamana bagli kararlarini tarar (<c>OfflineDeviceChecker</c> kalibi): suresi dolan siren talebi,
/// kart bekleme suresi dolan oturum, azami sureyi asan oturum. Isi KENDISI yapmaz — kararlari ilgili kabinin
/// kuyruguna birakir; motor ayni seritte, ingest'le yarismadan isler.
/// <para>Durum veritabaninda tutulur (<c>*DueAtUtc</c> alanlari): uygulama yeniden baslasa da zamanlayici kaybolmaz.
/// Hassasiyet tarama araligi kadardir (±2 sn).</para>
/// </summary>
public sealed class SignalTimerWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly SignalEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalTimerWorker> _logger;

    public SignalTimerWorker(SignalEventQueue queue, IServiceScopeFactory scopeFactory, ILogger<SignalTimerWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalTimerWorker basladi: her {Interval} sn", Interval.TotalSeconds);

        using var timer = new PeriodicTimer(Interval);

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
                _logger.LogError(exception, "SignalTimerWorker turu basarisiz");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalizationDbContext>();
        var now = DateTime.UtcNow;

        // Siren talebi kapanmis oturumlarda da acik kalamaz (kapanis talebi birakir); yine de EndedAtUtc'ye bakilmaz:
        // acik kalmis bir talep her kosulda kapanmali ki kabin sireni susabilsin.
        var sirenDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.SirenRequestedAtUtc != null && s.SirenReleasedAtUtc == null && s.SirenOffDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in sirenDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.SirenDue));

        var awaitingCardDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.AwaitingCardDueAtUtc != null && s.AwaitingCardDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in awaitingCardDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.AwaitingCardDue));

        var maxDurationDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.MaxDurationDueAtUtc != null && s.MaxDurationDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in maxDurationDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.MaxDurationDue));
    }
}
