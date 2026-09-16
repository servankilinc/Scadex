using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Kabin sireni: kabin basina TEK ve ORTAK. Oturumlar yalnizca TALEP acar/kapatir; fiziksel siren
/// <see cref="ReconcileSirenAsync"/> ile "en az bir acik talep var mi" sorusuna uzlastirilir.
/// Talep: kartla kilitleme basarili VE dis kapinin ardindaki TUM aktif ic kapilar kilitliyse acilir
/// (<see cref="AreAllInnerDoorsLockedAsync"/>) — baska bir kurum hala islem yapiyorsa (kilitsiz ic kapi varsa)
/// acilmaz. Kapanma: <c>SirenDurationSec</c> dolunca, dis kapi kapaninca ya da ayni dis kapinin ardinda
/// kilit yeniden acilinca.
/// </summary>
public partial class OperatorSessionEngine
{
    /// <summary> Oturum adına siren talebi açar </summary>
    private OperatorSessionEvent RequestSiren(OperatorSession session, SignalCabinet cabinet)
    {
        var now = DateTime.UtcNow;
        session.SirenRequestedAtUtc = now;
        session.SirenOffDueAtUtc = now.AddSeconds(Math.Max(1, cabinet.SirenDurationSec));
        session.SirenReleasedAtUtc = null;
        return AddEvent(session, SessionEventType.SirenRequested, now);
    }

    /// <summary> Oturumun açık siren talebini kapatır; SCADA'ya komut GONDERMEZ. Talep kapaninca kabinde baska acik talep yoksa siren ReconcileSirenAsync ile susar </summary>
    private OperatorSessionEvent? ReleaseSiren(OperatorSession session, string reason)
    {
        if (!session.HasActiveSirenRequest)
            return null;

        var now = DateTime.UtcNow;
        session.SirenReleasedAtUtc = now;
        return AddEvent(session, SessionEventType.SirenReleased, now, detail: reason);
    }

    /// <summary>
    /// Kabinin fiziksel sirenini oturum taleplerine uzlastirir: kabinde en az bir acik talep varsa siren acik,
    /// yoksa kapali olmalidir. Istenen durum son bilinen durumdan (<see cref="SignalCabinetState.SirenIsOn"/>)
    /// farkliysa siren kanalina TEK komut gonderilir; ayniysa komut gitmez
    /// </summary>
    private async Task ReconcileSirenAsync(SignalCabinet cabinet, OperatorSession? contextSession, OperatorSessionEvent? triggerEvent, CancellationToken cancellationToken)
    {
        if (cabinet.SirenIoChannelId is not Guid sirenChannelId)
            return;

        bool desired = await _db.OperatorSessions.AnyAsync(s =>
            s.CabinetId == cabinet.CabinetId &&
            s.SirenRequestedAtUtc != null &&
            s.SirenReleasedAtUtc == null, 
            cancellationToken
        );

        var state = await _db.CabinetStates.FirstOrDefaultAsync(s => s.CabinetId == cabinet.CabinetId, cancellationToken);
        if (state == null)
        {
            state = new SignalCabinetState { 
                CabinetId = cabinet.CabinetId, 
                SirenIsOn = false 
            };
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
                MarkFlag(contextSession, SessionFlags.CommandFailed, true);
                AddEvent(contextSession, SessionEventType.CommandFailed, now, deviceCommandId: outcome.CommandId,
                    detail: $"{SessionEventDetail.Siren}: {outcome.Message}");
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
