using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Enums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Kart okuma. Okuyucu kimligi yoktur: kabin MAC adresinden, ic kapi kullanicinin KURUMUNDAN cozulur
/// (tek kurum → kabinde tek ic kapi). Kapi bazinda gecis: kilitliyse ac; kilitsiz ve anahtar kapaliysa kilitle;
/// kilitsiz ve anahtar aciksa kilitleme (kapi fiziksel olarak kapanmadan kilit dilini surmek anlamsiz).
/// </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleCardAsync(SignalCabinet cabinet, CardPresentedNotification card, CancellationToken cancellationToken)
    {
        // 1) Kart -> kullanici (cekirdek cozdu). Tanimsiz kart da kayda gecer: ham kimlik guvenlik incelemesi icin gerekir.
        if (card.UserId is not Guid userId)
        {
            await DenyAsync(cabinet, card, SessionEventDetail.UnknownCard, cancellationToken);
            return;
        }

        // 2) Kullanici -> kurum (tek)
        var (authority, denyReason) = await ResolveAuthorityAsync(userId, cancellationToken);
        if (authority == null)
        {
            await DenyAsync(cabinet, card, denyReason!, cancellationToken);
            return;
        }

        // 3) Kurum -> bu kabindeki ic kapi (kabinde her kurumun en fazla bir aktif ic kapisi var)
        var inner = await _db.InnerDoors.AsNoTracking().Include(i => i.OuterDoor).FirstOrDefaultAsync(i =>
            i.IsActive &&
            i.AuthorityId == authority.Id &&
            i.OuterDoor!.IsActive &&
            i.OuterDoor.CabinetId == cabinet.CabinetId, cancellationToken);

        if (inner == null)
        {
            await DenyAsync(cabinet, card, SessionEventDetail.NoDoorForAuthority, cancellationToken);
            return;
        }

        // 4) Ic kapinin dis kapisinin oturumu. Oturum yoksa dis kapi mesaji kaybolmustur: operatoru kapida
        //    bekletmek daha kotu oldugu icin oturum ortuk acilir ve bayrakla isaretlenir.
        var outer = inner.OuterDoor!;
        var session = await GetOpenSessionAsync(outer.Id, cancellationToken) ?? StartSession(cabinet, outer, card.OccurredAtUtc, SessionFlags.OuterOpenMissing);

        // Yetkili kart okundu: "kartsiz giris" sayaci durur.
        session.AwaitingCardDueAtUtc = null;
        await UpsertOperatorAsync(session, userId, card, authority.Name, cancellationToken);
        AddEvent(session, SessionEventType.CardPresented, card.OccurredAtUtc, card.ReceivedAtUtc, innerDoorId: inner.Id, userId: userId, cardIdRaw: card.CardIdRaw);

        // 5) Kapi bazinda gecis
        OperatorSessionEvent? sirenEvent = null;
        var state = await GetOrCreateStateAsync(inner.Id, cancellationToken);

        if (!state.IsUnlocked)
        {
            bool unlocked = await UnlockInnerDoorAsync(session, inner, state, userId, cancellationToken);

            // Siren calarken ayni dis kapinin ardinda biri yeniden iceri giriyor: talep kapanir.
            if (unlocked)
                sirenEvent = ReleaseSiren(session, SessionEventDetail.UnlockedAgain);
        }
        else
        {
            var switchOpen = await IsSwitchOpenAsync(inner.SwitchIoChannelId, inner.SwitchOpenValue, cancellationToken);

            if (switchOpen == false)
            {
                bool locked = await LockInnerDoorAsync(session, inner, state, userId, SessionEventType.Locked, cancellationToken);
                if (locked)
                {
                    // "Hepsi kilitli mi" sorusu kalici duruma bakar: once yaz.
                    await _db.SaveChangesAsync(cancellationToken);

                    // Siren "islem bitti, dis kapiyi kapat" demektir: ardinda hala calisan biri varsa calmaz.
                    if (cabinet.SirenIoChannelId != null && await AreAllInnerDoorsLockedAsync(outer.Id, cancellationToken))
                        sirenEvent = RequestSiren(session, cabinet);
                }
            }
            else
            {
                AddEvent(session, SessionEventType.LockSkippedDoorOpen, DateTime.UtcNow, innerDoorId: inner.Id, userId: userId,
                    detail: switchOpen == null ? SessionEventDetail.SwitchUnknown : null);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ReconcileSirenAsync(cabinet, session, sirenEvent, cancellationToken);
    }

    /// <summary>
    /// Kullanicinin kurumu: aktif rolleri ile aktif kurumlarin rolleri kesisimi. Tek olmali — iki kurum rolu bir
    /// yapilandirma hatasidir (operatör ekrani bunu engeller, /admin/users'tan elle atanmis olabilir).
    /// </summary>
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
            cancellationToken: cancellationToken) ?? [];

        var authorities = await _db.Authorities.AsNoTracking()
            .Where(a => a.IsActive && activeRoleIds.Contains(a.RoleId))
            .ToListAsync(cancellationToken);

        if (authorities.Count > 1)
            _logger.LogWarning("Kullanici {UserId} birden fazla kurum rolune sahip ({Authorities}); kart reddedildi.", userId, string.Join(", ", authorities.Select(a => a.Name)));

        return authorities.Count switch
        {
            0 => (null, SessionEventDetail.NoAuthority),
            1 => (authorities[0], null),
            _ => (null, SessionEventDetail.MultipleAuthorities)
        };
    }

    /// <summary>
    /// Reddedilen kart. Hangi dis kapiya ait oldugu bilinemez (kapi kurumdan cozulur); kabindeki en son acilan
    /// acik oturuma yazilir. Acik oturum yoksa satir dogmaz, uyari loglanir — reddedilen kart icin oturum ACILMAZ.
    /// </summary>
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

        AddEvent(session, SessionEventType.AccessDenied, card.OccurredAtUtc, card.ReceivedAtUtc, userId: card.UserId, cardIdRaw: card.CardIdRaw, detail: reason);
        await _db.SaveChangesAsync(cancellationToken);
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
}
