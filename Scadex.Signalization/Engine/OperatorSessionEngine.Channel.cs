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

            // 3) Dış kapının ilişkili olduğu bir kamera varsa HER açılışta kayıt talebinde bulunulur (yalnızca yeni
            //    oturumda değil): iş bitmeden kapanan kapı oturumu açık bıraktığı için ikinci giriş de tanıksız kalmamalı.
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

            // 4) (17:00–08:00) Açılan dış kapının ilişkili ledi açılır
            if (IsLightingHours(DateTime.Now))
                await ReconcileOuterDoorLightAsync(outer, session, desired: true, cancellationToken);
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

            // 2) Dış kapının kapandı event kaydını at.
            var outerClosedEvent = AddEvent(session, SessionEventType.OuterClosed, occurredAtUtc, receivedAtUtc);

            // 3) Aktif çalan bir siren varsa kabinde sustur
            var sirenEvent = ReleaseSiren(session, SessionEventDetail.OuterClosed);

            // 4) Açık kalmış iç kapılar varsa bul ve kilitle (fiziksel güvenlik davranışı değişmez)
            await AutoLockInnerDoorsAsync(session, outer, cancellationToken);

            // 5) Oturum ve eventler kalıcı kaydedilir. Sıra önemli: AreAllInnerDoorsLockedAsync DB'ye
            //    sorar, otomatik kilitlemenin izlenen degisikliklerini ancak yazildiktan SONRA gorur.
            await _db.SaveChangesAsync(cancellationToken);

            // 6) Operatör işini bitirdi mi: dış kapının ardındaki tüm iç kapılar kilitliyse bitmiştir.
            bool finished = await AreAllInnerDoorsLockedAsync(outer.Id, cancellationToken);
            if (!finished)
                outerClosedEvent.Detail = SessionEventDetail.Unfinished;

            // 7) Oturumu bitiren şey kapının kapanması DEĞİL, işin bitmesidir: iş bitmediyse operatör
            //    (alet almaya çıkmış olabilir) geri dönebilsin diye oturum AÇIK kalır ve yeniden açılış
            //    aynı oturuma yazılır. Dönmezse oturumu SessionMaxDurationMin zamanlayıcısı kapatır.
            if (finished)
                await CloseSessionAsync(session, occurredAtUtc, timedOut: false, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);

            // 8) Aydinlatma SAAT BAKILMADAN sondurulur: 17:30'da yanan LED, sabah 08:10'da kapanan kapiyla da sonmeli. Zaten sonukse komut gitmez.
            await ReconcileOuterDoorLightAsync(outer, session, desired: false, cancellationToken);
        }
    }

    private async Task HandleInnerSwitchAsync(SignalCabinet cabinet, SignalInnerDoor inner, bool isOpen, DateTime occurredAtUtc, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var outerDoor = inner.OuterDoor!;
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.OuterDoorId == outerDoor.Id && s.EndedAtUtc == null, cancellationToken);

        // switch inputu kapının açıldığını bildirdiyse
        if (isOpen)
        {
            // 1) İç kapı ancak dış kapı açıkken açılabilir bu nedenle oturum kaydı yoksa açılmalı. Bu tarz süreçler guvenlik acisindan kaydedilmeden gecilemez.
            if (session == null)
            {
                // Dış kapı açılışını kaçırmış olabiliriz; ama switch "kapalı" diyorsa dış kapı hiç açılmadan iç kapı açılmış demektir — kaçırılmış olay değil, gerçek bir anomali.
                if (await IsSwitchOpenAsync(outerDoor.SwitchIoChannelId, outerDoor.SwitchOpenValue, cancellationToken) == false)
                    _logger.LogWarning("Kabin {CabinetId}: '{Outer}' switch'i kapali gorunurken ic kapi '{Inner}' acildi; oturum ortuk acildi.", cabinet.CabinetId, outerDoor.Name, inner.Name);

                session = StartSession(cabinet, outerDoor, occurredAtUtc, SessionFlags.OuterOpenMissing);
            }

            // 2) İç kapı kilidine en son hangi komut gönderildi (kayıt yoksa kapı kilitli sayılır).
            var innerDoorState = await GetOrInnerDoorStateAsync(inner.Id, cancellationToken);

            // 3) Son gönderilen komut da kilidi aç ise Switch'in kapı açıldı bilgisini tetiklemesi zaten beklenen bir durum
            if (innerDoorState.IsUnlocked)
            {
                AddEvent(
                    session: session,
                    type: SessionEventType.InnerOpened,
                    occurredAtUtc: occurredAtUtc,
                    receivedAtUtc: receivedAtUtc,
                    innerDoorId: inner.Id
                );
            }
            // 4) Kapının kilide komut gönderilmeden açılması kayıt altına alınmalı
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

                // 5) Switch "açık" diyorsa kilit fiilen TUTMUYOR demektir;
                innerDoorState.IsUnlocked = true;
                innerDoorState.ChangedAtUtc = receivedAtUtc;   // olayın ReceivedAtUtc'si ile AYNI damga: kart yolundaki
                                                               // "bu komuttan beri açıldı mı" karşılaştırması buna dayanıyor
                innerDoorState.LastCommandId = null;           // bu değişim bir komuttan doğmadı
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
    /// <summary>
    /// Oturum sonunda (dış kapı kapandı ya da azami süre doldu) ardındaki iç kapılar elden geçirilir:
    /// switch'i "kapalı" gösteren kilitsiz kapılar kilitlenir, açık duranlar bayraklanır.
    /// <para/>
    /// Sıra bilinçlidir: ÖNCE switch okunur. Switch "açık" ise kilit fiilen tutmuyor demektir ve bu, kaydı
    /// yalanlayan KESİN bilgidir — bu yüzden kayıt kontrolünden önce gelir. Switch "kapalı"/"bilinmiyor" ise
    /// kilidin durumu hakkında bir şey söylemez ve orada kayıt tek kaynaktır.
    /// </summary>
    private async Task AutoLockInnerDoorsAsync(OperatorSession session, SignalOuterDoor outer, CancellationToken cancellationToken)
    {
        var innerDoors = await _db.InnerDoors.AsNoTracking().Where(i => i.OuterDoorId == outer.Id && i.IsActive).ToListAsync(cancellationToken);

        foreach (var inner in innerDoors)
        {
            // 1) Once SAHA
            var switchOpen = await IsSwitchOpenAsync(inner.SwitchIoChannelId, inner.SwitchOpenValue, cancellationToken);

            // 2) İç kapı fiziksel olarak açık
            if (switchOpen == true)
            {
                var now = DateTime.UtcNow;
                var indoorState = await GetOrInnerDoorStateAsync(inner.Id, cancellationToken);

                // iç kapı durumu "kilitli" diyorsa yalanlanmış oluyor. (Switch kapanma durumunu islenmemis olabilir, kabin o sirada pasifti...
                if (!indoorState.IsUnlocked)
                {
                    indoorState.IsUnlocked = true;
                    indoorState.ChangedAtUtc = now;     // asagidaki ForcedOpen olayiyla AYNI damga olmali
                    indoorState.LastCommandId = null;   // bu degisim bir komuttan doğmadı

                    MarkFlag(session, SessionFlags.ForcedOpen);
                    AddEvent(session: session, type: SessionEventType.ForcedOpen, occurredAtUtc: now, receivedAtUtc: now, innerDoorId: inner.Id);
                    _logger.LogWarning("Kabin {CabinetId}: ic kapi '{Door}' switch'i acik ama kayit kilitli gosteriyordu; kayit duzeltildi.", session.CabinetId, inner.Name);
                }

                // Oturum sonu gercegi: kapi acik kaldi, kilitlenemedi. Komut GONDERILMEZ (acik kapiya kilit dili surulmez).
                MarkFlag(session, SessionFlags.InnerDoorLeftOpen);
                AddEvent(
                    session: session,
                    type: SessionEventType.LockSkippedDoorOpen,
                    occurredAtUtc: now,
                    innerDoorId: inner.Id,
                    detail: SessionEventDetail.SessionEnd
                );
                continue;
            }

            // 3) Switch "kapali" ya da "bilinmiyor": kilidin durumunu SOYLEMEZ, kayit tek kaynak.
            var state = await _db.InnerDoorStates.FirstOrDefaultAsync(s => s.InnerDoorId == inner.Id, cancellationToken);
            if (state?.IsUnlocked != true)
                continue;

            if (switchOpen == false)
            {
                await LockInnerDoorAsync(session, inner, state, userId: null, SessionEventType.AutoLocked, cancellationToken);
            }
            else // null — switch okunamadi, kilitlemeye kalkismayiz
            {
                MarkFlag(session, SessionFlags.InnerDoorLeftOpen);
                AddEvent(
                    session: session,
                    type: SessionEventType.LockSkippedDoorOpen,
                    occurredAtUtc: DateTime.UtcNow,
                    innerDoorId: inner.Id,
                    detail: SessionEventDetail.SwitchUnknown
                );
            }
        }
    }

    private async Task<bool> UnlockInnerDoorAsync(OperatorSession session, SignalInnerDoor inner, SignalInnerDoorState state, Guid? userId, string? detail, CancellationToken cancellationToken)
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
            deviceCommandId: outcome.CommandId,
            detail: detail
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
