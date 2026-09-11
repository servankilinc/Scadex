using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Runtime;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary> Dis kapi anahtari: oturumu acar ve kapatir. </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleOuterSwitchAsync(SignalCabinet cabinet, SignalOuterDoor outer, bool isOpen, DateTime occurredAtUtc, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var session = await GetOpenSessionAsync(outer.Id, cancellationToken);

        if (isOpen)
        {
            // Acik oturum varken gelen ikinci acilis yeni oturum acmaz; yalnizca olay olarak kayda gecer.
            bool isNew = session == null;
            session ??= StartSession(cabinet, outer, occurredAtUtc, SessionFlags.None);
            AddEvent(session, SessionEventType.OuterOpened, occurredAtUtc, receivedAtUtc);
            await _db.SaveChangesAsync(cancellationToken);

            // Kareler seridin DISINDA cekilir: 5 x 1 sn'lik seri ayni kabinin kart okumasini geciktirmemeli.
            if (isNew && outer.CameraId is Guid cameraId && cabinet.EntrySnapshotCount > 0)
                _snapshotQueue.Enqueue(new EntrySnapshotJob(session.Id, cameraId, cabinet.EntrySnapshotCount, Math.Max(100, cabinet.EntrySnapshotIntervalMs), DateTime.UtcNow));

            return;
        }

        if (session == null)
        {
            _logger.LogInformation("Kabin {CabinetId}: '{Door}' kapandi ama acik oturum yok; atlandi.", cabinet.CabinetId, outer.Name);
            return;
        }

        AddEvent(session, SessionEventType.OuterClosed, occurredAtUtc, receivedAtUtc);
        var sirenEvent = ReleaseSiren(session, SessionEventDetail.OuterClosed);

        await AutoLockInnerDoorsAsync(session, outer, cancellationToken);

        // Operator satirlari CloseSessionAsync'te okunur; oncesinde kalici olmali.
        await _db.SaveChangesAsync(cancellationToken);
        await CloseSessionAsync(session, occurredAtUtc, timedOut: false, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);
    }

    /// <summary>
    /// Dis kapi kapanirken ardindaki kilitsiz ic kapilar: anahtari "kapali" gosteren kilitlenir (<c>AutoLocked</c>, siren
    /// talebi ACILMAZ — dis kapi zaten kapali); acik ya da bilinmeyen kapi kilitlenmez, oturum uyari bayragi alir.
    /// </summary>
    private async Task AutoLockInnerDoorsAsync(OperatorSession session, SignalOuterDoor outer, CancellationToken cancellationToken)
    {
        var innerDoors = await _db.InnerDoors.AsNoTracking()
            .Where(i => i.OuterDoorId == outer.Id && i.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var inner in innerDoors)
        {
            var state = await _db.InnerDoorStates.FirstOrDefaultAsync(s => s.InnerDoorId == inner.Id, cancellationToken);
            if (state?.IsUnlocked != true)
                continue;

            var switchOpen = await IsSwitchOpenAsync(inner.SwitchIoChannelId, inner.SwitchOpenValue, cancellationToken);
            if (switchOpen == false)
            {
                await LockInnerDoorAsync(session, inner, state, userId: null, SessionEventType.AutoLocked, cancellationToken);
            }
            else
            {
                session.Flags |= SessionFlags.InnerDoorLeftOpen;
                AddEvent(session, SessionEventType.LockSkippedDoorOpen, DateTime.UtcNow, innerDoorId: inner.Id,
                    detail: switchOpen == null ? SessionEventDetail.SwitchUnknown : SessionEventDetail.SessionEnd);
            }
        }
    }
}
