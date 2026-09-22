using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Doğrulanan kart için: 
/// a) iç kapı kilitliyse aç, 
/// b) iç kapı kilitsiz ve switch kapalıysa kilitle; 
/// c) iç kapı kilitsiz ve switch açıksa(fiziksel olarka kapatılmadıysa) görmezden gel
/// </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleCardAsync(SignalCabinet cabinet, CardPresentedNotification card, CancellationToken cancellationToken)
    {
        // 1) Scadex çekirdeğinden kartın sahibi operatörün UserId bilgisi gelmeli
        if (card.UserId is not Guid userId)
        {
            await DenyAsync(cabinet, card, SessionEventDetail.UnknownCard, cancellationToken);
            return;
        }

        // 2) Operatörün rolleri içinde bir tane SignalAuthirity ile ilişkili kaydı olmalı bu sayede Sinyalizasyon modülündeki bir kurum ile ilişkilendirilecek
        var (authority, denyReason) = await ResolveAuthorityAsync(userId, cancellationToken);
        if (authority == null)
        {
            await DenyAsync(cabinet, card, denyReason!, cancellationToken);
            return;
        }

        // 3) Kurumun rolüne ait iç kapı ve ilişkili dış kapı bulunur
        var innerDoor = await _db.InnerDoors.AsNoTracking().Include(i => i.OuterDoor)
            .FirstOrDefaultAsync(i =>
                i.IsActive &&
                i.AuthorityId == authority.Id &&
                i.OuterDoor!.IsActive &&
                i.OuterDoor.CabinetId == cabinet.CabinetId,
                cancellationToken
            );
        if (innerDoor == null)
        {
            await DenyAsync(cabinet, card, SessionEventDetail.NoDoorForAuthority, cancellationToken);
            return;
        }
        var outerDoor = innerDoor.OuterDoor!;

        // 4) Kart okutma ancak dış kapı açıkken yapılabilir bu nedenle oturum kaydı yoksa açılmalı. Bu tarz süreçler guvenlik acisindan kaydedilmeden gecilemez.
        var session = await _db.OperatorSessions.FirstOrDefaultAsync(s => s.OuterDoorId == outerDoor.Id && s.EndedAtUtc == null, cancellationToken);
        string? implicitSessionDetail = null;
        if (session == null)
        {
            // Dış kapı açılışını kaçırmış olabiliriz Ama switch "kapalı" diyorsa kapı hiç açılmadan kart okutulmuş demektir;
            if ((await _signalizationChannelState.GetSwitchStateAsync(outerDoor, cancellationToken)).IsOn == false)
            {
                implicitSessionDetail = SessionEventDetail.OuterSwitchClosed;
                _logger.LogWarning("Kabin {CabinetId}: '{Door}' switch'i kapali gorunurken kart okutuldu; oturum ortuk acildi.", cabinet.CabinetId, outerDoor.Name);
            }

            session = StartSession(cabinet, outerDoor, card.OccurredAtUtc, SessionFlags.OuterOpenMissing);
        }


        // 5) Yetkili kartı okundu: "kartsiz giris" sayacı durur.
        session.AwaitingCardDueAtUtc = null;
        await UpsertOperatorAsync(session, userId, card, authority.Name, cancellationToken);
        AddEvent(
            session: session,
            type: SessionEventType.CardPresented,
            occurredAtUtc: card.OccurredAtUtc,
            receivedAtUtc: card.ReceivedAtUtc,
            innerDoorId: innerDoor.Id,
            userId: userId,
            cardIdRaw: card.CardIdRaw,
            detail: implicitSessionDetail
        );

        // 6) İç kapı kilidinin durumu kilit kanalından okunur ("bilinmiyor" kilitli muamelesi görür).
        var lockState = await _signalizationChannelState.GetLockStateAsync(innerDoor, cancellationToken);
        bool isUnlocked = lockState.IsOn == true; // kilitliyse veya bilinmiyorsa false

        // 7) Kapının fiziksel durumu.
        var switchOpen = (await _signalizationChannelState.GetSwitchStateAsync(innerDoor, cancellationToken)).IsOn;

        // 8) Kilit kanalı "açık" diyor ama kapı kilit açıldığından beri hiç açılmadıysa o komut fiilen UYGULANMAMIŞ kilit sıkışmış vs. demektir.
        bool openedSinceCommand =
            isUnlocked &&
            session.Id != 0 &&
            lockState.ChangedAtUtc is DateTime unlockedAtUtc &&
            await _db.OperatorSessionEvents.AnyAsync(e =>
                e.SessionId == session.Id &&
                e.InnerDoorId == innerDoor.Id &&
                (e.Type == SessionEventType.InnerOpened || e.Type == SessionEventType.ForcedOpen) &&
                e.ReceivedAtUtc >= unlockedAtUtc,
                cancellationToken
            );

        OperatorSessionEvent? sirenEvent = null;

        // 9) Kilidi aç koşulu: kanal "kilitli" diyorsa (ya da hiç bilmiyorsak) veya kapı şu an açık değil(switch kontrolü) ve iç kapı kilit aç komutundan sonra switch tarafından hiç kapı açıldığına dair bir bilgi gelmediyse açılmamış olabilir,operatör panoya ulaşabilsin.
        if (!isUnlocked || (switchOpen == false && !openedSinceCommand))
        {
            bool unlocked = await UnlockInnerDoorAsync(
                session, innerDoor, userId,
                detail: isUnlocked ? SessionEventDetail.UnlockNotEffective : null,
                cancellationToken
            );

            // Siren çalıyorsa talep kapanir (siren operatör işlemini bitirdikten sonra dış kapıyı kapatana kadar çalar, biri bu arada iceri giriyor durumu)
            if (unlocked)
                sirenEvent = ReleaseSiren(session, SessionEventDetail.UnlockedAgain);
        }
        // 10) Kilitle koşulu: kilit kanalı "açık", kapı bu komuttan beri açılmış ve switch şimdi kapalı — operatör işlemini bitirdi. İç kapıyı kilitle ve çıkması için sireni çalıştır.
        else if (switchOpen == false)
        {
            bool locked = await LockInnerDoorAsync(session, innerDoor, userId, SessionEventType.Locked, cancellationToken);
            if (locked)
            {
                // event yaz db ye
                await _db.SaveChangesAsync(cancellationToken);

                // Kabinde hala çalışılan (kilitsiz) başka iç kapı varsa çalmaz
                if (cabinet.SirenIoChannelId != null && await _signalizationChannelState.AreAllInnerDoorsLockedAsync(outerDoor.Id, cancellationToken))
                    sirenEvent = RequestSiren(session, cabinet);
            }
        }
        // 11) Switch "açık" ya da durumu bilinmiyor: kapı açıkken kilitlenmez.
        else
        {
            AddEvent(
                session: session,
                type: SessionEventType.LockSkippedDoorOpen,
                occurredAtUtc: DateTime.UtcNow,
                innerDoorId: innerDoor.Id,
                userId: userId,
                detail: switchOpen == null ? SessionEventDetail.SwitchUnknown : null
            );
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);

        // Operatörler kartını her okuttuğunda bir kamera kaydı yakalıyorum
        if (outerDoor.CameraId is Guid cardCameraId)
            _snapshotQueue.Enqueue(new EntrySnapshotWorkItem(session.Id, cardCameraId, 1, 0, DateTime.UtcNow, innerDoorId: innerDoor.Id));
    }


    #region Helpers
    private async Task<(SignalAuthority? Authority, string? DenyReason)> ResolveAuthorityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleNamesResult = await _userRoleService.GetRolesOfUserAsync(userId, cancellationToken);
        if (!roleNamesResult.IsSuccess || roleNamesResult.Data.Count == 0)
            return (null, SessionEventDetail.NoAuthority);

        var roleNames = roleNamesResult.Data.ToList();

        // Pasif rol yetki turetmez (AuthService.GetPermissionCodesAsync ile ayni ilke).
        var activeRoleIds = await _unitOfWork.Roles.GetAllAsync(
            select: r => r.Id,
            where: r => r.IsActive && roleNames.Contains(r.Name!),
            cancellationToken: cancellationToken
        ) ?? [];

        // Kullanıcının rolleri içinde sadece bir tane SiganlAuth kaydı varsa o rolün kapısı açılır
        var authorities = await _db.Authorities.AsNoTracking().Where(a => a.IsActive && activeRoleIds.Contains(a.RoleId)).ToListAsync(cancellationToken);

        if (authorities.Count > 1)
            _logger.LogWarning("Kullanici {UserId} birden fazla kurum rolune sahip ({Authorities}); kart reddedildi.", userId, string.Join(", ", authorities.Select(a => a.Name)));

        return authorities.Count switch
        {
            0 => (null, SessionEventDetail.NoAuthority),
            1 => (authorities[0], null),
            _ => (null, SessionEventDetail.MultipleAuthorities)
        };
    }

    private async Task DenyAsync(SignalCabinet cabinet, CardPresentedNotification card, string reason, CancellationToken cancellationToken)
    {
        var session = await _db.OperatorSessions
            .Where(s => s.CabinetId == cabinet.CabinetId && s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (session == null)
        {
            _logger.LogWarning("Kabin {CabinetId}: kart {CardId} reddedildi ({Reason}) ve acik oturum yok; yalnizca loglandi.", cabinet.CabinetId, card.CardIdRaw, reason);
            return;
        }

        AddEvent(
            session: session,
            type: SessionEventType.AccessDenied,
            occurredAtUtc: card.OccurredAtUtc,
            receivedAtUtc: card.ReceivedAtUtc,
            userId: card.UserId,
            cardIdRaw: card.CardIdRaw,
            detail: reason
        );
        await _db.SaveChangesAsync(cancellationToken);

        // Reddedilen kart hangi ic kapiya aitti cozulemedi; kamera dis kapidan (session.OuterDoorId) gelir.
        var cameraId = await _db.OuterDoors.AsNoTracking()
            .Where(o => o.Id == session.OuterDoorId)
            .Select(o => o.CameraId)
            .FirstOrDefaultAsync(cancellationToken);

        // reddedilen kart için kamera kaydı alınır
        if (cameraId is Guid denyCameraId)
            _snapshotQueue.Enqueue(new EntrySnapshotWorkItem(session.Id, denyCameraId, 1, 0, DateTime.UtcNow));
    }

    private async Task UpsertOperatorAsync(OperatorSession session, Guid userId, CardPresentedNotification card, string authorityName, CancellationToken cancellationToken)
    {
        OperatorSessionOperator? sessionOperator = null;

        // Yeni (henuz kaydedilmemis) oturumun operatoru olamaz.
        if (session.Id != 0)
            sessionOperator = await _db.OperatorSessionOperators.FirstOrDefaultAsync(o => o.SessionId == session.Id && o.UserId == userId, cancellationToken);

        if (sessionOperator != null)
        {
            sessionOperator.LastCardAtUtc = card.OccurredAtUtc;
            return;
        }

        _db.OperatorSessionOperators.Add(new OperatorSessionOperator
        {
            Session = session,
            UserId = userId,
            FullNameSnapshot = card.UserFullName ?? string.Empty,
            AuthorityNameSnapshot = authorityName,
            CardIdRaw = card.CardIdRaw,
            FirstCardAtUtc = card.OccurredAtUtc,
            LastCardAtUtc = card.OccurredAtUtc
        });
    }
    #endregion
}
