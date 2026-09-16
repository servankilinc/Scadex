using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

public partial class OperatorSessionEngine
{
    private async Task HandleChannelChangedAsync(SignalCabinet cabinet, ChannelChangedWork work, CancellationToken cancellationToken)
    {
        // 1) Kanal değişim bilgisi gelmiş mi
        var notification = work.Notification;
        if (notification.Value == null)
            return;

        // 2) Kanal kabinin dış kapısının switch inputu mu?
        var outerDoor = await _db.OuterDoors.AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.CabinetId == cabinet.CabinetId &&
                d.IsActive &&
                d.SwitchIoChannelId == notification.IoChannelId,
                cancellationToken
            );
        if (outerDoor != null)
        {
            bool isOpen = string.Equals(notification.Value, outerDoor.SwitchOpenValue, StringComparison.Ordinal);
            await HandleOuterSwitchAsync(cabinet, outerDoor, isOpen, notification.OccurredAtUtc, notification.ReceivedAtUtc, cancellationToken);
            return;
        }

        // 3) Kanal kabinin bir iç kapısının switch inputu mu?
        var innerDoor = await _db.InnerDoors.AsNoTracking().Include(i => i.OuterDoor)
            .FirstOrDefaultAsync(i =>
                i.IsActive &&
                i.SwitchIoChannelId == notification.IoChannelId &&
                i.OuterDoor!.IsActive &&
                i.OuterDoor.CabinetId == cabinet.CabinetId,
                cancellationToken
            );
        if (innerDoor != null)
        {
            bool isOpen = string.Equals(notification.Value, innerDoor.SwitchOpenValue, StringComparison.Ordinal);
            await HandleInnerSwitchAsync(cabinet, innerDoor, isOpen, notification.OccurredAtUtc, notification.ReceivedAtUtc, cancellationToken);
        }

        // şu an başka bir input kanalını işlemiyoruz ileride gerekirse bu metoda eklenecek...
    }

    #region Channel Methods
    private async Task HandleOuterSwitchAsync(SignalCabinet cabinet, SignalOuterDoor outer, bool isOpen, DateTime occurredAtUtc, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.OuterDoorId == outer.Id && s.EndedAtUtc == null, cancellationToken);

        // Dış kapı switch inputu kapının açıldığını bildirdiyse
        if (isOpen)
        {
            // 1) Açık oturum varken gelen ikinci kapı açılışı yeni oturum acmaz; yalnızca event hareketi olarak kayda geçer
            session ??= StartSession(cabinet, outer, occurredAtUtc, SessionFlags.None);

            // 2) Event hareketi kaydedilir (yeni oturumun Id'si de burada olusur)
            AddEvent(
                session: session,
                type: SessionEventType.OuterOpened,
                occurredAtUtc: occurredAtUtc,
                receivedAtUtc: receivedAtUtc
            );
            await _db.SaveChangesAsync(cancellationToken);

            // 3) Eğer oturum yeni açıldıysa ve dış kapının ilişkili olduğu bir kamera varsa kayıt talebinde bulunulur
            if (outer.CameraId.HasValue && cabinet.EntrySnapshotCount > 0)
            {
                _snapshotQueue.Enqueue(
                    new EntrySnapshotWorkItem(
                        sessionId: session.Id,
                        cameraId: outer.CameraId.Value,
                        count: cabinet.EntrySnapshotCount,
                        intervalMs: Math.Max(100, cabinet.EntrySnapshotIntervalMs),
                        startAtUtc: DateTime.UtcNow
                    )
                );
            }
        }
        // Dış kapı switch inputu kapının kapatıldığını bildirdiyse
        else
        {
            // 1) Açık oturum yoksa switch yanlış tetiklenmiş olabilir log kaydı atılması yeterli
            if (session == null)
            {
                _logger.LogInformation("Kabin {CabinetId}: '{Door}' kapandı ama açik oturum yok; bir sorun var incelenmesi gerek.", cabinet.CabinetId, outer.Name);
                return;
            }

            // 2) Operatör işini bitirdi mi: kart ile kilitleme tamamlandıysa bitmiştir.
            bool finished = await AreAllInnerDoorsLockedAsync(outer.Id, cancellationToken);

            // 3) Dış kapının kapnadı event kaydını at; iş bitmemişse gerekçe Detail'e yazılır
            AddEvent(
                session, 
                SessionEventType.OuterClosed, 
                occurredAtUtc, receivedAtUtc,
                detail: finished ? null : SessionEventDetail.Unfinished
            );

            // 4) Aktif çalan bir siren varsa kabinde sustur
            var sirenEvent = ReleaseSiren(session, SessionEventDetail.OuterClosed);

            // 5) Açık kalmış iç kapılar varsa bul ve kilitle (fiziksel güvenlik davranışı değişmez)
            await AutoLockInnerDoorsAsync(session, outer, cancellationToken);

            // 6) Oturum ve eventler kalıcı kaydedilir
            await _db.SaveChangesAsync(cancellationToken);

            // 7) Oturumu bitiren şey kapının kapanması DEĞİL, işin bitmesidir: iş bitmediyse operatör
            //    (alet almaya çıkmış olabilir) geri dönebilsin diye oturum AÇIK kalır ve yeniden açılış
            //    aynı oturuma yazılır. Dönmezse oturumu SessionMaxDurationMin zamanlayıcısı kapatır.
            if (finished)
            {
                await CloseSessionAsync(session, occurredAtUtc, timedOut: false, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }

            await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);
        }
    }

    private async Task HandleInnerSwitchAsync(SignalCabinet cabinet, SignalInnerDoor inner, bool isOpen, DateTime occurredAtUtc, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var outerDoor = inner.OuterDoor!;
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.OuterDoorId == outerDoor.Id && s.EndedAtUtc == null, cancellationToken);
        var innerDoorState = await _db.InnerDoorStates.AsNoTracking().FirstOrDefaultAsync(s => s.InnerDoorId == inner.Id, cancellationToken);

        // İç kapı kilidine en son aç komutu mu gönderilm 
        bool isUnlocked = innerDoorState?.IsUnlocked == true;

        // switch inputu kapının açıldığını bildirdiyse
        if (isOpen)
        {
            // 1) İç kapı ancak dış kapı açıkken açılabilir bu nedenle oturum kaydı yoksa açılmalı. Bu tarz süreçler guvenlik acisindan kaydedilmeden gecilemez.
            session ??= StartSession(cabinet, outerDoor, occurredAtUtc, SessionFlags.OuterOpenMissing);

            // 2) Son gönderilen komut da kilidi aç ise Switch'in kapı açıldı bilgisini tetiklemesi zaten beklenen bir durum
            if (isUnlocked)
            {
                AddEvent(
                    session: session,
                    type: SessionEventType.InnerOpened,
                    occurredAtUtc: occurredAtUtc,
                    receivedAtUtc: receivedAtUtc,
                    innerDoorId: inner.Id
                );
            }
            // 3) Kapının kilide komut gönderilmeden açılması kayıt altına alınmalı
            else
            {
                MarkFlag(session, SessionFlags.ForcedOpen);
                AddEvent(
                    session: session,
                    type: SessionEventType.ForcedOpen,
                    occurredAtUtc: occurredAtUtc,
                    receivedAtUtc: receivedAtUtc,
                    innerDoorId: inner.Id
                );
                _logger.LogWarning("Kabin {CabinetId}: kilitli ic kapi '{Door}' acildi (zorlanmis acilis).", cabinet.CabinetId, inner.Name);
            }
        }
        else
        {
            // 1) Açılan iç kapının dış kapısının tamamlanmamış bir oturumu yoksa iç kapının kapatılması önemli değil
            if (session == null)
                return;

            // 2) İç kapı kapatıldı event hareket kaydı atılır
            AddEvent(
                session: session,
                type: SessionEventType.InnerClosed,
                occurredAtUtc: occurredAtUtc,
                receivedAtUtc: receivedAtUtc,
                innerDoorId: inner.Id
            );
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
    #endregion

    #region Heplers
    /// <summary> Dış kapı kapanırken ardındaki kilitsiz iç kapılar dan switch "kapalı" gosterenler kilitlenir </summary>
    private async Task AutoLockInnerDoorsAsync(OperatorSession session, SignalOuterDoor outer, CancellationToken cancellationToken)
    {
        var innerDoors = await _db.InnerDoors.AsNoTracking().Where(i => i.OuterDoorId == outer.Id && i.IsActive).ToListAsync(cancellationToken);

        foreach (var inner in innerDoors)
        {
            // 1) İç kapı kilidine gönderilen son komut kilitle ise atlanır
            var state = await _db.InnerDoorStates.FirstOrDefaultAsync(s => s.InnerDoorId == inner.Id, cancellationToken);
            if (state?.IsUnlocked != true)
                continue;

            // 2) Kilide gönderilen son komut bilinmiyor veya kilitle değilse kapının switch inputu en son ne göndermişti diye kontrol edilir
            var switchOpen = await IsSwitchOpenAsync(inner.SwitchIoChannelId, inner.SwitchOpenValue, cancellationToken);
            if (switchOpen == false)
            {
                await LockInnerDoorAsync(session, inner, state, userId: null, SessionEventType.AutoLocked, cancellationToken);
            }
            else
            {
                MarkFlag(session, SessionFlags.InnerDoorLeftOpen);
                AddEvent(
                    session: session,
                    type: SessionEventType.LockSkippedDoorOpen,
                    occurredAtUtc: DateTime.UtcNow,
                    innerDoorId: inner.Id,
                    detail: switchOpen == null ? SessionEventDetail.SwitchUnknown : SessionEventDetail.SessionEnd
                );
            }
        }
    }

    private async Task<bool> UnlockInnerDoorAsync(OperatorSession session, SignalInnerDoor inner, SignalInnerDoorState state, Guid? userId, CancellationToken cancellationToken)
    {
        // Komut SCADA'nin zaman asimi kadar surebilir: oncesindeki olaylar (kart okuma) kalici olsun.
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(inner.LockIoChannelId, turnOn: inner.UnlockTurnsOn, cancellationToken);
        var now = DateTime.UtcNow;

        if (!outcome.IsSuccess)
        {
            MarkFlag(session, SessionFlags.CommandFailed);
            AddEvent(
                session: session,
                type: SessionEventType.CommandFailed,
                occurredAtUtc: now,
                innerDoorId: inner.Id,
                userId: userId,
                deviceCommandId: outcome.CommandId,
                detail: $"{SessionEventDetail.Lock}: {outcome.Message}"
            );
            return false;
        }

        state.IsUnlocked = true;
        state.ChangedAtUtc = now;
        state.LastCommandId = outcome.CommandId;
        AddEvent(
            session: session,
            type: SessionEventType.Unlocked,
            occurredAtUtc: now,
            innerDoorId: inner.Id,
            userId: userId,
            deviceCommandId: outcome.CommandId
        );
        return true;
    }

    private async Task<bool> LockInnerDoorAsync(
        OperatorSession session,
        SignalInnerDoor inner,
        SignalInnerDoorState state,
        Guid? userId,
        SessionEventType eventType,
        CancellationToken cancellationToken
    )
    {
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(inner.LockIoChannelId, turnOn: !inner.UnlockTurnsOn, cancellationToken);
        var now = DateTime.UtcNow;

        if (!outcome.IsSuccess)
        {
            MarkFlag(session, SessionFlags.CommandFailed, true);
            AddEvent(
                session: session,
                type: SessionEventType.CommandFailed,
                occurredAtUtc: now,
                innerDoorId: inner.Id,
                userId: userId,
                deviceCommandId: outcome.CommandId,
                detail: $"{SessionEventDetail.Lock}: {outcome.Message}"
            );
            return false;
        }

        state.IsUnlocked = false;
        state.ChangedAtUtc = now;
        state.LastCommandId = outcome.CommandId;
        AddEvent(
            session: session,
            type: eventType,
            occurredAtUtc: now,
            innerDoorId: inner.Id,
            userId: userId,
            deviceCommandId: outcome.CommandId
        );
        return true;
    }
    #endregion
}
