using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Queue;

namespace Scadex.Signalization.BackgroundServices;

/// <summary> 
/// Operatör işlem oturumlarının zamana bagli işler(TimeWork) üretir.
/// Süresi dolan siren talebi, kart bekleme suresi dolan oturum, azami sureyi aşan oturum vb. süreçleri kontrol eder. İş yapmaz ilgili kabinin kuyruğuna bırakır
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

        #region Otomatik siren susturma zamanlayıcısı
        // Siren isteği varsa & siren susturlmadıysa & ve otomatik susuturluma zamanı geldiyse => koşulunu sağlayan oturumları bul
        var sirenDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.SirenRequestedAtUtc != null && s.SirenReleasedAtUtc == null && s.SirenOffDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in sirenDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.SirenDue));
        #endregion

        #region Geç kalınmış kart okutma kontrolcüsü (yetkisiz dış kapı açılmasını takip eder)
        // Tamamlanmamış işlemlerde kart okutma süresi aşılmış taleplerde bu durumun hareket kaydı oluşturulur.
        // NOT: İşlem oturumlarında kart okutulduğunda AwaitingCardDueAtUtc(kart son beklenme tarihi) null'e çekilir
        var awaitingCardDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.AwaitingCardDueAtUtc != null && s.AwaitingCardDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in awaitingCardDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.AwaitingCardDue));
        #endregion

        #region İşlem belirtilen süre içerisinde tamamlanmadı mı kontrolcüsü
        var maxDurationDue = await db.OperatorSessions.AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.MaxDurationDueAtUtc != null && s.MaxDurationDueAtUtc <= now)
            .Select(s => new { s.Id, s.CabinetId })
            .ToListAsync(cancellationToken);

        foreach (var s in maxDurationDue)
            _queue.TryEnqueueTimer(new TimerWork(s.CabinetId, s.Id, SignalTimerKind.MaxDurationDue));
        #endregion
    }
}
