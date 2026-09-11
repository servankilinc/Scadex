using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Enums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Kabin sireni: kabin basina TEK ve ORTAK. Oturumlar yalnizca TALEP acar/kapatir; fiziksel siren
/// <see cref="ReconcileSirenAsync"/> ile "en az bir acik talep var mi" sorusuna uzlastirilir.
/// </summary>
public partial class OperatorSessionEngine
{
    private OperatorSessionEvent RequestSiren(OperatorSession session, SignalCabinet cabinet)
    {
        var now = DateTime.UtcNow;
        session.SirenRequestedAtUtc = now;
        session.SirenOffDueAtUtc = now.AddSeconds(Math.Max(1, cabinet.SirenDurationSec));
        session.SirenReleasedAtUtc = null;
        return AddEvent(session, SessionEventType.SirenRequested, now);
    }

    /// <returns> Acik talep yoksa <c>null</c> (olay yazilmaz). </returns>
    private OperatorSessionEvent? ReleaseSiren(OperatorSession session, string reason)
    {
        if (!session.HasActiveSirenRequest)
            return null;

        var now = DateTime.UtcNow;
        session.SirenReleasedAtUtc = now;
        return AddEvent(session, SessionEventType.SirenReleased, now, detail: reason);
    }

    /// <summary>
    /// Istenen durum (kabinde acik talep var mi) fiziksel durumdan farkliysa TEK komut gonderir. Boylece ikinci bir
    /// dis kapinin talebi calan sirene ikinci "ac" komutu gondermez; bir talebin kapanmasi digeri acikken susturmaz.
    /// <para>Komut basarisizsa fiziksel durum degismez ve <c>CommandFailed</c> yazilir. Retry dongusu YOKTUR: uzlastirma
    /// bir sonraki talep degisiminde (olay gudumlu) yeniden denenir.</para>
    /// <para>Cagirmadan once talep degisiklikleri kaydedilmis olmali — istenen durum veritabanindan okunur.</para>
    /// </summary>
    /// <param name="triggerEvent"> Uzlastirmayi tetikleyen talep olayi; komut giderse kimligi bu olaya islenir. </param>
    private async Task ReconcileSirenAsync(SignalCabinet cabinet, OperatorSession? contextSession, OperatorSessionEvent? triggerEvent, CancellationToken cancellationToken)
    {
        if (cabinet.SirenIoChannelId is not Guid sirenChannelId)
            return;

        bool desired = await _db.OperatorSessions.AnyAsync(s =>
            s.CabinetId == cabinet.CabinetId &&
            s.SirenRequestedAtUtc != null &&
            s.SirenReleasedAtUtc == null, cancellationToken);

        var state = await _db.CabinetStates.FirstOrDefaultAsync(s => s.CabinetId == cabinet.CabinetId, cancellationToken);
        if (state == null)
        {
            state = new SignalCabinetState { CabinetId = cabinet.CabinetId, SirenIsOn = false };
            _db.CabinetStates.Add(state);
        }

        if (state.SirenIsOn == desired)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var outcome = await SendOutputAsync(sirenChannelId, turnOn: desired, cancellationToken);
        var now = DateTime.UtcNow;

        if (outcome.IsSuccess)
        {
            state.SirenIsOn = desired;
            state.SirenChangedAtUtc = now;
            state.LastSirenCommandId = outcome.CommandId;

            if (triggerEvent != null)
                triggerEvent.DeviceCommandId = outcome.CommandId;
        }
        else
        {
            _logger.LogWarning("Kabin {CabinetId}: siren {Action} komutu basarisiz: {Message}", cabinet.CabinetId, desired ? "acma" : "susturma", outcome.Message);

            if (contextSession != null)
            {
                MarkFlag(contextSession, SessionFlags.CommandFailed);
                AddEvent(contextSession, SessionEventType.CommandFailed, now, deviceCommandId: outcome.CommandId,
                    detail: $"{SessionEventDetail.Siren}: {outcome.Message}");
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
