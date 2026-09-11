using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Dtos.Session.Queries;
using Scadex.Signalization.Entities;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Rapor sorgulari: sayfali liste, oturum detayi, donem ozeti. </summary>
public partial class OperatorSessionService
{
    /// <inheritdoc />
    public async Task<Result<PaginationResponse<OperatorSessionListItemDto>>> GetPagedAsync(OperatorSessionQueryRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<PaginationResponse<OperatorSessionListItemDto>>.Validation(validationResult.Failures, description: "Validation failed for OperatorSessionQueryRequest");

        var query = _db.OperatorSessions.AsNoTracking().AsQueryable();

        if (request.CabinetId is Guid cabinetId) query = query.Where(s => s.CabinetId == cabinetId);
        if (request.OuterDoorId is Guid outerDoorId) query = query.Where(s => s.OuterDoorId == outerDoorId);
        if (request.UserId is Guid userId) query = query.Where(s => s.Operators!.Any(o => o.UserId == userId));
        if (request.FromUtc is DateTime from) query = query.Where(s => s.StartedAtUtc >= from);
        if (request.ToUtc is DateTime to) query = query.Where(s => s.StartedAtUtc <= to);
        if (request.Status is OperatorSessionStatus status) query = query.Where(s => s.Status == status);
        if (request.Flags is SessionFlags flags && flags != SessionFlags.None) query = query.Where(s => (s.Flags & flags) != 0);

        // Kurum filtresi operatorun kurum ENSTANTANESINE (ada) uygulanir ve kurumun BUGUNKU adiyla aranir:
        // kurum sonradan yeniden adlandirildiysa eski addaki oturumlar bu filtreye takilmaz (bilinen sinir).
        if (request.AuthorityId is Guid authorityId)
        {
            var authorityName = await _db.Authorities.AsNoTracking().Where(a => a.Id == authorityId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken);
            query = authorityName == null ? query.Where(s => false) : query.Where(s => s.Operators!.Any(o => o.AuthorityNameSnapshot == authorityName));
        }

        var page = await ProjectListItems(query.OrderByDescending(s => s.StartedAtUtc).ThenByDescending(s => s.Id))
            .AsSplitQuery()
            .ToPaginateAsync(new PaginationRequest { Page = request.Page, PageSize = request.PageSize }, cancellationToken);

        var cabinetNames = await LoadCabinetNamesAsync(page.Data.Select(r => r.CabinetId), cancellationToken);
        foreach (var row in page.Data)
            row.CabinetName = cabinetNames.GetValueOrDefault(row.CabinetId);

        return Result<PaginationResponse<OperatorSessionListItemDto>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result<OperatorSessionDetailDto>> GetDetailAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.OperatorSessions.AsNoTracking()
            .Include(s => s.Operators)
            .Include(s => s.Events)
            .Include(s => s.Captures)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            return Result<OperatorSessionDetailDto>.NotFound(description: "Oturum bulunamadı");

        var operators = (session.Operators ?? []).OrderBy(o => o.FirstCardAtUtc).ToList();
        var events = (session.Events ?? []).OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).ToList();
        var captureLinks = (session.Captures ?? []).OrderBy(c => c.Sequence).ToList();

        // Kapi adlari (pasif kapilar dahil — gecmis kayit o kapiyi gosterir)
        var doorIds = events.Where(e => e.InnerDoorId != null).Select(e => e.InnerDoorId!.Value).Distinct().ToList();
        var doors = await _db.InnerDoors.AsNoTracking()
            .Where(i => doorIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Name, AuthorityName = i.Authority!.Name })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        // Ad: once oturumdaki enstantane, yoksa (reddedilen kullanici) cekirdekteki bugunku ad
        var operatorNames = operators.ToDictionary(o => o.UserId, o => o.FullNameSnapshot);
        var otherUserIds = events.Where(e => e.UserId != null && !operatorNames.ContainsKey(e.UserId.Value)).Select(e => e.UserId!.Value).ToList();
        var otherNames = await LoadUserNamesAsync(otherUserIds, cancellationToken);
        string? NameOf(Guid? id) => id is Guid g ? operatorNames.GetValueOrDefault(g) ?? otherNames.GetValueOrDefault(g) : null;

        var captureIds = captureLinks.Select(c => c.CameraCaptureId).ToList();
        var captures = (await _unitOfWork.CameraCaptures.GetAllAsync(
            select: c => new { c.Id, c.Status, c.CapturedAtUtc, c.RelativePath, c.FailureReason },
            where: c => captureIds.Contains(c.Id),
            cancellationToken: cancellationToken) ?? []).ToDictionary(c => c.Id);

        var cabinetNames = await LoadCabinetNamesAsync([session.CabinetId], cancellationToken);

        var dto = new OperatorSessionDetailDto
        {
            Id = session.Id,
            CabinetId = session.CabinetId,
            CabinetName = cabinetNames.GetValueOrDefault(session.CabinetId),
            OuterDoorId = session.OuterDoorId,
            OuterDoorName = session.OuterDoorNameSnapshot,
            Status = session.Status,
            Flags = session.Flags,
            StartedAtUtc = session.StartedAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            DurationSec = session.DurationSec,
            CaptureCount = captureLinks.Count,
            HasAlert = (session.Flags & AlertFlags) != 0,
            SirenRequestedAtUtc = session.SirenRequestedAtUtc,
            SirenReleasedAtUtc = session.SirenReleasedAtUtc,
            Operators = operators.Select(o => new OperatorSessionOperatorDto
            {
                UserId = o.UserId,
                FullName = o.FullNameSnapshot,
                AuthorityName = o.AuthorityNameSnapshot,
                CardIdRaw = o.CardIdRaw,
                FirstCardAtUtc = o.FirstCardAtUtc,
                LastCardAtUtc = o.LastCardAtUtc
            }).ToList(),
            Events = events.Select(e => new OperatorSessionEventDto
            {
                Id = e.Id,
                Type = e.Type,
                OccurredAtUtc = e.OccurredAtUtc,
                ReceivedAtUtc = e.ReceivedAtUtc,
                InnerDoorId = e.InnerDoorId,
                InnerDoorName = e.InnerDoorId is Guid d && doors.TryGetValue(d, out var door) ? door.Name : null,
                UserId = e.UserId,
                UserFullName = NameOf(e.UserId),
                CardIdRaw = e.CardIdRaw,
                DeviceCommandId = e.DeviceCommandId,
                CameraCaptureId = e.CameraCaptureId,
                Detail = e.Detail
            }).ToList(),
            Doors = BuildDoorSummaries(events, doorIds.Select(id => (id, doors.TryGetValue(id, out var d) ? d.Name : "?", doors.TryGetValue(id, out var d2) ? d2.AuthorityName : null))),
            Captures = captureLinks.Select(link =>
            {
                captures.TryGetValue(link.CameraCaptureId, out var capture);
                return new OperatorSessionCaptureDto
                {
                    CameraCaptureId = link.CameraCaptureId,
                    Sequence = link.Sequence,
                    Status = capture?.Status,
                    CapturedAtUtc = capture?.CapturedAtUtc,
                    RelativePath = capture?.RelativePath,
                    FailureReason = capture?.FailureReason
                };
            }).ToList()
        };

        return Result<OperatorSessionDetailDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<Result<OperatorSessionSummaryDto>> GetSummaryAsync(OperatorSessionSummaryRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<OperatorSessionSummaryDto>.Validation(validationResult.Failures, description: "Validation failed for OperatorSessionSummaryRequest");

        // Ozet TAMAMLANMIS isi olcer: acik oturumun suresi henuz yok, sayilmaz.
        var query = _db.OperatorSessions.AsNoTracking()
            .Where(s => s.EndedAtUtc != null && s.StartedAtUtc >= request.FromUtc && s.StartedAtUtc <= request.ToUtc);
        if (request.CabinetId is Guid cabinetId)
            query = query.Where(s => s.CabinetId == cabinetId);

        var sessions = await query.Select(s => new { s.Id, s.CabinetId, Duration = s.DurationSec ?? 0, s.Flags }).ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var operators = await _db.OperatorSessionOperators.AsNoTracking()
            .Where(o => sessionIds.Contains(o.SessionId))
            .Select(o => new { o.SessionId, o.UserId, o.FullNameSnapshot, o.AuthorityNameSnapshot, o.LastCardAtUtc })
            .ToListAsync(cancellationToken);

        var sessionById = sessions.ToDictionary(s => s.Id);
        var cabinetNames = await LoadCabinetNamesAsync(sessions.Select(s => s.CabinetId), cancellationToken);

        OperatorSessionSummaryRowDto Row(string key, string name, IEnumerable<long> ids)
        {
            var group = ids.Distinct().Select(id => sessionById[id]).ToList();
            int total = group.Sum(s => s.Duration);
            return new OperatorSessionSummaryRowDto
            {
                Key = key,
                Name = name,
                SessionCount = group.Count,
                TotalDurationSec = total,
                AverageDurationSec = group.Count == 0 ? 0 : total / group.Count,
                WarningCount = group.Count(s => s.Flags != SessionFlags.None)
            };
        }

        var dto = new OperatorSessionSummaryDto
        {
            SessionCount = sessions.Count,
            TotalDurationSec = sessions.Sum(s => s.Duration),
            WarningCount = sessions.Count(s => s.Flags != SessionFlags.None),
            ByOperator = operators
                .GroupBy(o => o.UserId)
                .Select(g => Row(g.Key.ToString(), g.OrderByDescending(o => o.LastCardAtUtc).First().FullNameSnapshot, g.Select(o => o.SessionId)))
                .OrderByDescending(r => r.TotalDurationSec)
                .ToList(),
            ByAuthority = operators
                .GroupBy(o => o.AuthorityNameSnapshot)
                .Select(g => Row(g.Key, g.Key, g.Select(o => o.SessionId)))
                .OrderByDescending(r => r.TotalDurationSec)
                .ToList(),
            ByCabinet = sessions
                .GroupBy(s => s.CabinetId)
                .Select(g => Row(g.Key.ToString(), cabinetNames.GetValueOrDefault(g.Key) ?? g.Key.ToString(), g.Select(s => s.Id)))
                .OrderByDescending(r => r.TotalDurationSec)
                .ToList()
        };

        return Result<OperatorSessionSummaryDto>.Success(dto);
    }

    private static IQueryable<OperatorSessionListItemDto> ProjectListItems(IQueryable<OperatorSession> query) => query.Select(s => new OperatorSessionListItemDto
    {
        Id = s.Id,
        CabinetId = s.CabinetId,
        OuterDoorId = s.OuterDoorId,
        OuterDoorName = s.OuterDoorNameSnapshot,
        Status = s.Status,
        Flags = s.Flags,
        StartedAtUtc = s.StartedAtUtc,
        EndedAtUtc = s.EndedAtUtc,
        DurationSec = s.DurationSec,
        CaptureCount = s.Captures!.Count(),
        HasAlert = (s.Flags & AlertFlags) != 0,
        Operators = s.Operators!.OrderBy(o => o.FirstCardAtUtc).Select(o => new OperatorSessionOperatorDto
        {
            UserId = o.UserId,
            FullName = o.FullNameSnapshot,
            AuthorityName = o.AuthorityNameSnapshot,
            CardIdRaw = o.CardIdRaw,
            FirstCardAtUtc = o.FirstCardAtUtc,
            LastCardAtUtc = o.LastCardAtUtc
        }).ToList()
    });

    /// <summary> Kapi basina ozet OLAYLARDAN turetilir: ayni kapi bir oturumda birden fazla kez acilabilir. </summary>
    private static List<OperatorSessionDoorDto> BuildDoorSummaries(List<OperatorSessionEvent> events, IEnumerable<(Guid Id, string Name, string? AuthorityName)> doors)
    {
        return doors.Select(door =>
        {
            var doorEvents = events.Where(e => e.InnerDoorId == door.Id).ToList();
            var opens = doorEvents.Where(e => e.Type is SessionEventType.InnerOpened or SessionEventType.ForcedOpen).ToList();

            return new OperatorSessionDoorDto
            {
                InnerDoorId = door.Id,
                Name = door.Name,
                AuthorityName = door.AuthorityName,
                FirstUnlockedAtUtc = doorEvents.Where(e => e.Type == SessionEventType.Unlocked).Select(e => (DateTime?)e.OccurredAtUtc).Min(),
                FirstOpenedAtUtc = opens.Select(e => (DateTime?)e.OccurredAtUtc).Min(),
                LastClosedAtUtc = doorEvents.Where(e => e.Type == SessionEventType.InnerClosed).Select(e => (DateTime?)e.OccurredAtUtc).Max(),
                LastLockedAtUtc = doorEvents.Where(e => e.Type is SessionEventType.Locked or SessionEventType.AutoLocked).Select(e => (DateTime?)e.OccurredAtUtc).Max(),
                OpenCount = opens.Count,
                WasForcedOpen = opens.Any(e => e.Type == SessionEventType.ForcedOpen)
            };
        }).ToList();
    }
}
