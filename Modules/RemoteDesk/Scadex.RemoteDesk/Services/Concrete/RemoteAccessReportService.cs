using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.DataAccess;
using Scadex.RemoteDesk.Model.Dtos.Report.Queries;
using Scadex.RemoteDesk.Model.Entities;
using Scadex.RemoteDesk.Services.Abstract;

namespace Scadex.RemoteDesk.Services.Concrete;

/// <summary>
/// Denetim tabloları modülün context'inden okunur; PC ve kullanıcı ADLARI çekirdekten yalnızca projeksiyonla, sayfadaki kimliklerle
/// tek sorguda çözülür (FK yok, başka context). Çekirdekte artık olmayan kimliğin adı <c>null</c> döner, satır yine listelenir.
/// </summary>
public class RemoteAccessReportService : IRemoteAccessReportService
{
    private readonly RemoteDeskDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public RemoteAccessReportService(RemoteDeskDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<RemoteAccessFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        // Union = DISTINCT; PC ve kullanıcı sayısı küçüktür, satır sayısı ne olursa olsun liste kısa kalır.
        var deviceIds = await _db.ScreenViewLogs.Select(v => v.DeviceId)
            .Union(_db.RemoteControlSessions.Select(c => c.DeviceId))
            .ToListAsync(cancellationToken);
        var userIds = await _db.ScreenViewLogs.Select(v => v.UserId)
            .Union(_db.RemoteControlSessions.Select(c => c.UserId))
            .ToListAsync(cancellationToken);

        var devices = await LoadDevicesAsync(deviceIds, cancellationToken);
        var users = await LoadUsersAsync(userIds, cancellationToken);

        var options = new RemoteAccessFilterOptionsDto
        {
            Pcs = [.. deviceIds
                .Select(id => devices.TryGetValue(id, out var d)
                    ? new ReportPcOptionDto { DeviceId = id, DeviceName = d.Name, CabinetName = d.CabinetName }
                    : new ReportPcOptionDto { DeviceId = id })
                // Adı çözülemeyen (silinmiş) en sona.
                .OrderBy(p => p.DeviceName is null)
                .ThenBy(p => p.CabinetName)
                .ThenBy(p => p.DeviceName)],
            Users = [.. userIds
                .Select(id => users.TryGetValue(id, out var u)
                    ? new ReportUserOptionDto { UserId = id, FullName = u.FullName, UserName = u.UserName }
                    : new ReportUserOptionDto { UserId = id })
                .OrderBy(u => u.FullName is null)
                .ThenBy(u => u.FullName)]
        };

        return Result<RemoteAccessFilterOptionsDto>.Success(options);
    }

    /// <inheritdoc />
    public async Task<Result<PaginationResponse<ScreenViewReportItemDto>>> GetViewsAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default)
    {
        if (Validate(query) is { } failures)
            return Result<PaginationResponse<ScreenViewReportItemDto>>.Validation(failures, description: "Validation failed for RemoteAccessReportQuery");

        var page = await ApplyFilter(_db.ScreenViewLogs.AsNoTracking(), query)
            .OrderByDescending(v => v.StartedUtc)
            .ThenByDescending(v => v.Id)
            .Select(v => new ScreenViewReportItemDto
            {
                Id = v.Id,
                DeviceId = v.DeviceId,
                MonitorIndex = v.MonitorIndex,
                UserId = v.UserId,
                StartedUtc = v.StartedUtc,
                EndedUtc = v.EndedUtc
            })
            .ToPaginateAsync(query.ToPaginationRequest(), cancellationToken);

        var devices = await LoadDevicesAsync(page.Data.Select(r => r.DeviceId), cancellationToken);
        var users = await LoadUsersAsync(page.Data.Select(r => r.UserId), cancellationToken);
        foreach (var row in page.Data)
        {
            if (devices.TryGetValue(row.DeviceId, out var device))
                (row.DeviceName, row.CabinetName) = device;
            if (users.TryGetValue(row.UserId, out var user))
                (row.UserFullName, row.UserName) = user;
        }

        return Result<PaginationResponse<ScreenViewReportItemDto>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result<PaginationResponse<RemoteControlReportItemDto>>> GetControlsAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default)
    {
        if (Validate(query) is { } failures)
            return Result<PaginationResponse<RemoteControlReportItemDto>>.Validation(failures, description: "Validation failed for RemoteAccessReportQuery");

        var page = await ApplyFilter(_db.RemoteControlSessions.AsNoTracking(), query)
            .OrderByDescending(c => c.StartedUtc)
            .Select(c => new RemoteControlReportItemDto
            {
                Id = c.Id,
                DeviceId = c.DeviceId,
                UserId = c.UserId,
                StartedUtc = c.StartedUtc,
                EndedUtc = c.EndedUtc,
                EndReason = c.EndReason
            })
            .ToPaginateAsync(query.ToPaginationRequest(), cancellationToken);

        var devices = await LoadDevicesAsync(page.Data.Select(r => r.DeviceId), cancellationToken);
        var users = await LoadUsersAsync(page.Data.Select(r => r.UserId), cancellationToken);
        foreach (var row in page.Data)
        {
            if (devices.TryGetValue(row.DeviceId, out var device))
                (row.DeviceName, row.CabinetName) = device;
            if (users.TryGetValue(row.UserId, out var user))
                (row.UserFullName, row.UserName) = user;
        }

        return Result<PaginationResponse<RemoteControlReportItemDto>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result<RemoteAccessSummaryDto>> GetSummaryAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default)
    {
        if (Validate(query) is { } failures)
            return Result<RemoteAccessSummaryDto>.Validation(failures, description: "Validation failed for RemoteAccessReportQuery");

        // Açık kayıt (EndedUtc null) "şu ana kadar" sayılır; yetim kayıtlar açılışta kapandığı için null = gerçekten sürüyor.
        var now = DateTime.UtcNow;
        var views = ApplyFilter(_db.ScreenViewLogs.AsNoTracking(), query);
        var controls = ApplyFilter(_db.RemoteControlSessions.AsNoTracking(), query);

        var viewTotal = await TotalAsync(views, now, cancellationToken);
        var controlTotal = await TotalAsync(controls, now, cancellationToken);

        var topPc = await views
            .GroupBy(v => v.DeviceId)
            .Select(g => new { Id = g.Key, Seconds = g.Sum(v => (long)EF.Functions.DateDiffSecond(v.StartedUtc, v.EndedUtc ?? now)) })
            .OrderByDescending(x => x.Seconds)
            .FirstOrDefaultAsync(cancellationToken);
        var topUser = await views
            .GroupBy(v => v.UserId)
            .Select(g => new { Id = g.Key, Seconds = g.Sum(v => (long)EF.Functions.DateDiffSecond(v.StartedUtc, v.EndedUtc ?? now)) })
            .OrderByDescending(x => x.Seconds)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new RemoteAccessSummaryDto
        {
            ViewCount = viewTotal.Count,
            ViewSeconds = viewTotal.Seconds,
            ControlCount = controlTotal.Count,
            ControlSeconds = controlTotal.Seconds
        };

        if (topPc is not null)
        {
            var devices = await LoadDevicesAsync([topPc.Id], cancellationToken);
            summary.TopPc = new ReportPcTotalDto { DeviceId = topPc.Id, Seconds = topPc.Seconds };
            if (devices.TryGetValue(topPc.Id, out var device))
                (summary.TopPc.DeviceName, summary.TopPc.CabinetName) = device;
        }

        if (topUser is not null)
        {
            var users = await LoadUsersAsync([topUser.Id], cancellationToken);
            summary.TopUser = new ReportUserTotalDto { UserId = topUser.Id, Seconds = topUser.Seconds };
            if (users.TryGetValue(topUser.Id, out var user))
                (summary.TopUser.FullName, summary.TopUser.UserName) = user;
        }

        return Result<RemoteAccessSummaryDto>.Success(summary);
    }

    #region Helpers
    /// <summary> Tarih aralığı kaydın BAŞLANGICINA uygulanır (indeks ekseni); iki uç da dahil. </summary>
    private static IQueryable<T> ApplyFilter<T>(IQueryable<T> source, RemoteAccessReportQuery query) where T : class, IRemoteAccessRecord
    {
        if (query.DeviceId is { } deviceId)
            source = source.Where(r => r.DeviceId == deviceId);
        if (query.UserId is { } userId)
            source = source.Where(r => r.UserId == userId);
        if (query.FromUtc is { } from)
            source = source.Where(r => r.StartedUtc >= from);
        if (query.ToUtc is { } to)
            source = source.Where(r => r.StartedUtc <= to);
        return source;
    }

    private static async Task<(int Count, long Seconds)> TotalAsync<T>(IQueryable<T> source, DateTime now, CancellationToken cancellationToken)
        where T : class, IRemoteAccessRecord
    {
        // Tek gidiş: COUNT + SUM aynı sorguda. Kayıt yoksa satır da yoktur → (0, 0).
        var total = await source
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Seconds = g.Sum(r => (long)EF.Functions.DateDiffSecond(r.StartedUtc, r.EndedUtc ?? now)) })
            .FirstOrDefaultAsync(cancellationToken);
        return total is null ? (0, 0) : (total.Count, total.Seconds);
    }

    private static Dictionary<string, string[]>? Validate(RemoteAccessReportQuery query) =>
        query is { FromUtc: { } from, ToUtc: { } to } && to < from
            ? new() { [nameof(RemoteAccessReportQuery.ToUtc)] = ["Bitiş tarihi başlangıçtan önce olamaz"] }
            : null;

    /// <summary> Pasif cihazın adı da çözülür (<c>Device</c>'ta <c>IsActive</c> filtresi yok); silinmiş kabin <c>null</c> döner. </summary>
    private async Task<Dictionary<Guid, (string? Name, string? CabinetName)>> LoadDevicesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
            return [];

        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.Id, d.Name, CabinetName = d.Cabinet!.Name },
            where: d => distinct.Contains(d.Id),
            cancellationToken: cancellationToken) ?? [];
        return rows.ToDictionary(r => r.Id, r => ((string?)r.Name, (string?)r.CabinetName));
    }

    private async Task<Dictionary<Guid, (string? FullName, string? UserName)>> LoadUsersAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
            return [];

        var rows = await _unitOfWork.Users.GetAllAsync(
            select: u => new { u.Id, u.FullName, u.UserName },
            where: u => distinct.Contains(u.Id),
            cancellationToken: cancellationToken) ?? [];
        return rows.ToDictionary(r => r.Id, r => ((string?)r.FullName, r.UserName));
    }
    #endregion
}
