using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Queue;
using Scadex.Signalization.Realtime;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.BackgroundServices;

/// <summary>
/// Scadex çekirdeğin kanal değişimlerini (<c>IScadaEventObserver.OnChannelChangedAsync</c> ile gelen) sinyalizasyon modülünde anlamlı (hangi siren, aydınlatma ya da kilit)
/// SginalR yayınlarına çeviren worker servisi.
/// </summary>
public sealed class SignalRealtimeWorker : BackgroundService
{
    private readonly SignalRealtimeQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISignalizationNotifier _notifier;
    private readonly ILogger<SignalRealtimeWorker> _logger;

    public SignalRealtimeWorker(SignalRealtimeQueue queue, IServiceScopeFactory scopeFactory, ISignalizationNotifier notifier, ILogger<SignalRealtimeWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SignalizationDbContext>();

                    if (notification.Direction == PinDirection.Output)
                        await PublishOutputAsync(db, notification, stoppingToken);
                    else
                        await PublishSwitchAsync(db, notification, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // Tek bir yayının hatası sonrakileri durdurmamalı.
                    _logger.LogError(exception, "Kabin {CabinetId}: kanal {IoChannelId} canli yayina cevrilemedi", notification.CabinetId, notification.IoChannelId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary> Kapı anahtarı: kapının "açık" değeriyle SUNUCUDA yorumlanır. </summary>
    private async Task PublishSwitchAsync(SignalizationDbContext db, ChannelChangedNotification notification, CancellationToken cancellationToken)
    {
        var channelId = notification.IoChannelId;
        var cabinetId = notification.CabinetId;

        var outerDoors = await db.OuterDoors.AsNoTracking()
            .Where(d => d.CabinetId == cabinetId && d.IsActive && d.SwitchIoChannelId == channelId)
            .Select(d => new { d.Id, d.SwitchOpenValue })
            .ToListAsync(cancellationToken);

        var innerDoors = await db.InnerDoors.AsNoTracking()
            .Where(i => i.IsActive && i.SwitchIoChannelId == channelId && i.OuterDoor!.IsActive && i.OuterDoor.CabinetId == cabinetId)
            .Select(i => new { i.Id, i.SwitchOpenValue })
            .ToListAsync(cancellationToken);

        foreach (var door in outerDoors)
            await _notifier.SignalDoorSwitchChangedAsync(new SignalDoorSwitchChangedMessage(cabinetId, SignalDoorKind.Outer, door.Id, IsOpen(notification.Value, door.SwitchOpenValue), notification.ReceivedAtUtc), cancellationToken);

        foreach (var door in innerDoors)
            await _notifier.SignalDoorSwitchChangedAsync(new SignalDoorSwitchChangedMessage(cabinetId, SignalDoorKind.Inner, door.Id, IsOpen(notification.Value, door.SwitchOpenValue), notification.ReceivedAtUtc), cancellationToken);
    }

    /// <summary> Çıkış: çekirdeğin mantıksal değeri (<c>TurnOn</c>, NO/NC çevrilmeden önce); kilitte ayrıca <c>UnlockTurnsOn</c>. </summary>
    private async Task PublishOutputAsync(SignalizationDbContext db, ChannelChangedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.TurnOn is not bool turnOn)
            return;

        var channelId = notification.IoChannelId;
        var cabinetId = notification.CabinetId;
        var changedAtUtc = notification.ReceivedAtUtc;

        bool isSiren = await db.Cabinets.AsNoTracking().AnyAsync(c => c.CabinetId == cabinetId && c.SirenIoChannelId == channelId, cancellationToken);
        if (isSiren)
            await _notifier.SignalCabinetStateChangedAsync(new SignalCabinetStateChangedMessage(cabinetId, SignalCabinetOutput.Siren, null, turnOn, changedAtUtc), cancellationToken);

        var lightDoorIds = await db.OuterDoors.AsNoTracking()
            .Where(d => d.CabinetId == cabinetId && d.IsActive && d.LightIoChannelId == channelId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        foreach (var doorId in lightDoorIds)
            await _notifier.SignalCabinetStateChangedAsync(new SignalCabinetStateChangedMessage(cabinetId, SignalCabinetOutput.OuterDoorLight, doorId, turnOn, changedAtUtc), cancellationToken);

        var lockDoors = await db.InnerDoors.AsNoTracking()
            .Where(i => i.IsActive && i.LockIoChannelId == channelId && i.OuterDoor!.IsActive && i.OuterDoor.CabinetId == cabinetId)
            .Select(i => new { i.Id, i.UnlockTurnsOn })
            .ToListAsync(cancellationToken);

        // Kilidi açmak için hangi mantıksal değerin gittiği kilidin kendi mantığıdır.
        foreach (var door in lockDoors)
            await _notifier.SignalCabinetStateChangedAsync(new SignalCabinetStateChangedMessage(cabinetId, SignalCabinetOutput.InnerDoorLock, door.Id, turnOn == door.UnlockTurnsOn, changedAtUtc), cancellationToken);
    }

    private static bool? IsOpen(string? value, string openValue) =>
        value == null ? null : string.Equals(value, openValue, StringComparison.Ordinal);
}
