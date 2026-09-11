using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Enums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary> Ic kapi: anahtar dogrulamasi ve kilit komutlari. </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleInnerSwitchAsync(SignalCabinet cabinet, SignalInnerDoor inner, bool isOpen, DateTime occurredAtUtc, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var outer = inner.OuterDoor!;
        var session = await GetOpenSessionAsync(outer.Id, cancellationToken);
        var state = await _db.InnerDoorStates.AsNoTracking().FirstOrDefaultAsync(s => s.InnerDoorId == inner.Id, cancellationToken);
        bool isUnlocked = state?.IsUnlocked == true;

        if (isOpen)
        {
            // Ic kapi ancak dis kapi acikken acilabilir; oturum yoksa dis kapi mesaji kaybolmustur. Zorlanmis acilis
            // guvenlik acisindan kaydedilmeden gecilemez — oturum ortuk olarak acilir.
            session ??= StartSession(cabinet, outer, occurredAtUtc, SessionFlags.OuterOpenMissing);

            if (isUnlocked)
            {
                AddEvent(session, SessionEventType.InnerOpened, occurredAtUtc, receivedAtUtc, innerDoorId: inner.Id);
            }
            else
            {
                session.Flags |= SessionFlags.ForcedOpen;
                AddEvent(session, SessionEventType.ForcedOpen, occurredAtUtc, receivedAtUtc, innerDoorId: inner.Id);
                _logger.LogWarning("Kabin {CabinetId}: kilitli ic kapi '{Door}' acildi (zorlanmis acilis).", cabinet.CabinetId, inner.Name);
            }
        }
        else
        {
            if (session == null)
                return;

            AddEvent(session, SessionEventType.InnerClosed, occurredAtUtc, receivedAtUtc, innerDoorId: inner.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SignalInnerDoorState> GetOrCreateStateAsync(Guid innerDoorId, CancellationToken cancellationToken)
    {
        var state = await _db.InnerDoorStates.FirstOrDefaultAsync(s => s.InnerDoorId == innerDoorId, cancellationToken);
        if (state != null)
            return state;

        // Satir yoksa kapi kilitli sayilir.
        state = new SignalInnerDoorState { InnerDoorId = innerDoorId, IsUnlocked = false, ChangedAtUtc = DateTime.UtcNow };
        _db.InnerDoorStates.Add(state);
        return state;
    }

    /// <returns> Komut basariliysa <c>true</c>; basarisizsa kapinin durumu DEGISMEZ. </returns>
    private async Task<bool> UnlockInnerDoorAsync(OperatorSession session, SignalInnerDoor inner, SignalInnerDoorState state, Guid? userId, CancellationToken cancellationToken)
    {
        // Komut SCADA'nin zaman asimi kadar surebilir: oncesindeki olaylar (kart okuma) kalici olsun.
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(inner.LockIoChannelId, turnOn: inner.UnlockTurnsOn, cancellationToken);
        var now = DateTime.UtcNow;

        if (!outcome.IsSuccess)
        {
            session.Flags |= SessionFlags.CommandFailed;
            AddEvent(session, SessionEventType.CommandFailed, now, innerDoorId: inner.Id, userId: userId, deviceCommandId: outcome.CommandId,
                detail: $"{SessionEventDetail.Lock}: {outcome.Message}");
            return false;
        }

        state.IsUnlocked = true;
        state.ChangedAtUtc = now;
        state.LastCommandId = outcome.CommandId;
        AddEvent(session, SessionEventType.Unlocked, now, innerDoorId: inner.Id, userId: userId, deviceCommandId: outcome.CommandId);
        return true;
    }

    /// <returns> Komut basariliysa <c>true</c>; basarisizsa kapinin durumu DEGISMEZ. </returns>
    private async Task<bool> LockInnerDoorAsync(OperatorSession session, SignalInnerDoor inner, SignalInnerDoorState state, Guid? userId, SessionEventType eventType, CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(inner.LockIoChannelId, turnOn: !inner.UnlockTurnsOn, cancellationToken);
        var now = DateTime.UtcNow;

        if (!outcome.IsSuccess)
        {
            MarkFlag(session, SessionFlags.CommandFailed);
            AddEvent(session, SessionEventType.CommandFailed, now, innerDoorId: inner.Id, userId: userId, deviceCommandId: outcome.CommandId,
                detail: $"{SessionEventDetail.Lock}: {outcome.Message}");
            return false;
        }

        state.IsUnlocked = false;
        state.ChangedAtUtc = now;
        state.LastCommandId = outcome.CommandId;
        AddEvent(session, eventType, now, innerDoorId: inner.Id, userId: userId, deviceCommandId: outcome.CommandId);
        return true;
    }

    /// <summary> Dis kapinin ardindaki TUM aktif ic kapilar kilitli mi? Cagirmadan once durum degisiklikleri kaydedilmis olmali. </summary>
    private async Task<bool> AreAllInnerDoorsLockedAsync(Guid outerDoorId, CancellationToken cancellationToken)
    {
        var doorIds = await _db.InnerDoors
            .Where(i => i.OuterDoorId == outerDoorId && i.IsActive)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        return !await _db.InnerDoorStates.AnyAsync(s => doorIds.Contains(s.InnerDoorId) && s.IsUnlocked, cancellationToken);
    }
}
