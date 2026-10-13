using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Services.Concrete;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Yetkili kullanıcının açık işlemi elle kapatması. Akış zaman aşımının (<see cref="SignalTimerKind.MaxDurationDue"/>) aynasıdır:
/// siren talebi bırakılır, iç kapılara son bir otomatik kilitleme denenir, oturum <see cref="OperatorSessionStatus.ManuallyClosed"/> kapanır.
/// <para>
/// Talep kuyrukta beklerken motor oturumu kapatmış olabilir — koşul burada YENİDEN kontrol edilir.
/// </para>
/// </summary>
public partial class OperatorSessionEngine
{
    private async Task<Result> HandleManualCloseAsync(SignalCabinet cabinet, ManualCloseWork work, CancellationToken cancellationToken)
    {
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.Id == work.SessionId && s.CabinetId == cabinet.CabinetId, cancellationToken);
        if (session == null)
            return Result.NotFound(message: "İşlem bulunamadı.");

        if (session.EndedAtUtc != null)
            return OperatorSessionService.AlreadyClosed();

        var now = DateTime.UtcNow;

        // Önce kapatma kaydı: zaman çizelgesinde sonraki siren/kilit olaylarının nedeni olarak görünür.
        AddEvent(session, SessionEventType.ManuallyClosed, now, userId: work.UserId, detail: work.Reason.Trim());

        var sirenEvent = ReleaseSiren(session, SessionEventDetail.ManuallyClosed);

        // Zaman aşımındaki gibi: dış kapının ardında kilitsiz kalan iç kapılar için son bir deneme.
        var outer = await _db.OuterDoors.AsNoTracking().FirstOrDefaultAsync(o => o.Id == session.OuterDoorId, cancellationToken);
        if (outer != null)
            await AutoLockInnerDoorsAsync(session, outer, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await CloseSessionAsync(session, now, SessionCloseKind.Manual, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);

        _logger.LogInformation("Kabin {CabinetId}: islem {SessionId} kullanici {UserId} tarafindan elle kapatildi", cabinet.CabinetId, session.Id, work.UserId);
        return Result.Success();
    }
}
