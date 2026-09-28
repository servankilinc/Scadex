using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Model.Dtos.Session.Queries;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Kart okuyucusu: kabinin son kart okumalari. </summary>
public partial class OperatorSessionService
{
    /// <summary> Ekranin gosterdigi okuma sayisi. Liste bir gecmis ekrani degil; eskisi oturum raporundan bulunur. </summary>
    private const int RecentCardReadCount = 5;

    /// <inheritdoc />
    /// <remarks>
    /// Ayri bir okuma tablosu YOKTUR: okumalar oturum olaylaridir (<c>CardPresented</c> / <c>AccessDenied</c>)
    /// </remarks>
    public async Task<Result<ICollection<SignalCardReadDto>>> GetRecentCardReadsAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var events = await _db.OperatorSessionEvents.AsNoTracking()
            .Where(e =>
                (e.Type == SessionEventType.CardPresented || e.Type == SessionEventType.AccessDenied) &&
                e.Session!.CabinetId == cabinetId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Take(RecentCardReadCount)
            .Select(e => new { e.Id, e.SessionId, e.Type, e.OccurredAtUtc, e.CardIdRaw, e.UserId, e.InnerDoorId, e.Detail })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
            return Result<ICollection<SignalCardReadDto>>.Success([]);

        // Ad ve kurum: once okumanin oturumundaki enstantane (GetDetailAsync ile ayni kural).
        var sessionIds = events.Select(e => e.SessionId).Distinct().ToList();
        var userIds = events.Where(e => e.UserId != null).Select(e => e.UserId!.Value).Distinct().ToList();
        var operators = await _db.OperatorSessionOperators.AsNoTracking()
            .Where(o => sessionIds.Contains(o.SessionId) && userIds.Contains(o.UserId))
            .Select(o => new { o.SessionId, o.UserId, o.FullNameSnapshot, o.AuthorityNameSnapshot })
            .ToListAsync(cancellationToken);
        var snapshots = operators.ToDictionary(o => (o.SessionId, o.UserId));

        // Enstantanesi olmayan (reddedilen) kullanicilar icin cekirdekteki bugunku ad.
        var otherUserIds = events
            .Where(e => e.UserId is Guid u && !snapshots.ContainsKey((e.SessionId, u)))
            .Select(e => e.UserId!.Value);
        var otherNames = await LoadUserNamesAsync(otherUserIds, cancellationToken);

        // Kapi adlari (pasif kapilar dahil — gecmis kayit o kapiyi gosterir)
        var doorIds = events.Where(e => e.InnerDoorId != null).Select(e => e.InnerDoorId!.Value).Distinct().ToList();
        var doorNames = await _db.InnerDoors.AsNoTracking()
            .Where(i => doorIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

        var rows = events.Select(e =>
        {
            var snapshot = e.UserId is Guid userId ? snapshots.GetValueOrDefault((e.SessionId, userId)) : null;
            bool isAccepted = e.Type == SessionEventType.CardPresented;

            return new SignalCardReadDto
            {
                Id = e.Id,
                SessionId = e.SessionId,
                OccurredAtUtc = e.OccurredAtUtc,
                CardIdRaw = e.CardIdRaw,
                IsAccepted = isAccepted,
                UserId = e.UserId,
                UserFullName = snapshot?.FullNameSnapshot ?? (e.UserId is Guid u ? otherNames.GetValueOrDefault(u) : null),
                // Red okumasinda kurum yoktur: ayni kisinin oturumdaki (onceki kabulden kalan) kurumu bu okumaya ait degil.
                AuthorityName = isAccepted ? snapshot?.AuthorityNameSnapshot : null,
                InnerDoorName = e.InnerDoorId is Guid d ? doorNames.GetValueOrDefault(d) : null,
                Detail = e.Detail
            };
        }).ToList();

        return Result<ICollection<SignalCardReadDto>>.Success(rows);
    }
}
