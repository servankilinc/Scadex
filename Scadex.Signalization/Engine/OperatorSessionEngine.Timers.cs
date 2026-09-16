using Microsoft.EntityFrameworkCore;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Zamanlayicinin kararlari. Karar kuyrukta beklerken durum degismis olabilir (orn. siren talebi kart okumasiyla
/// kapandi) — kosul burada YENIDEN kontrol edilir; tutmuyorsa hicbir sey yapilmaz.
/// </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleTimerAsync(SignalCabinet cabinet, TimerWork work, CancellationToken cancellationToken)
    {
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.Id == work.SessionId, cancellationToken);
        if (session == null)
            return;

        var now = DateTime.UtcNow;

        switch (work.Kind)
        {
            case SignalTimerKind.SirenDue:
            {
                if (!session.HasActiveSirenRequest || session.SirenOffDueAtUtc > now)
                    return;

                var sirenEvent = ReleaseSiren(session, SessionEventDetail.Timeout);
                await _db.SaveChangesAsync(cancellationToken);
                await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);
                break;
            }

            case SignalTimerKind.AwaitingCardDue:
            {
                if (session.EndedAtUtc != null || session.AwaitingCardDueAtUtc == null || session.AwaitingCardDueAtUtc > now)
                    return;

                // Oturum KAPANMAZ: dis kapi kapanana kadar surer; gec okutulan kart normal islenir, bayrak kalir.
                session.AwaitingCardDueAtUtc = null;
                MarkFlag(session, SessionFlags.UnauthorizedEntry);
                AddEvent(session, SessionEventType.AwaitingCardTimedOut, now);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            }

            case SignalTimerKind.MaxDurationDue:
            {
                if (session.EndedAtUtc != null || session.MaxDurationDueAtUtc == null || session.MaxDurationDueAtUtc > now)
                    return;

                var sirenEvent = ReleaseSiren(session, SessionEventDetail.SessionTimedOut);

                // Dış kapı kapanırken switch'i "açık" olduğu için atlanan bir iç kapı bu arada
                // kapanmış olabilir. Denenmezse o kapı kalıcı olarak kilitsiz kalır.
                var outer = await _db.OuterDoors.AsNoTracking().FirstOrDefaultAsync(o => o.Id == session.OuterDoorId, cancellationToken);
                if (outer != null)
                    await AutoLockInnerDoorsAsync(session, outer, cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await CloseSessionAsync(session, now, timedOut: true, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);
                break;
            }
        }
    }
}
