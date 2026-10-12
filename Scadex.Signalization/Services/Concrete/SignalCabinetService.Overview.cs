using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Model.Dtos.Config.Queries;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Sinyalizasyon kabin tablosu: cekirdekteki her kabin + modul yapilandirmasi, siren durumu ve islem istatistigi. </summary>
public partial class SignalCabinetService
{
    /// <summary> Tablodaki "son 30 gun" sayilarinin penceresi. </summary>
    private const int OverviewStatsDays = 30;

    /// <inheritdoc />
    public async Task<Result<List<SignalCabinetOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        // Satir tabani cekirdek kabinlerdir (pasifler dahil, silinmisleri IsDeleted filtresi disarida birakir); cekirdek YALNIZCA projeksiyonla okunur.
        var cabinets = await _unitOfWork.Cabinets.GetAllAsync(select: c => new { c.Id, c.Name, c.IsActive }, cancellationToken: cancellationToken) ?? [];

        var configs = await _db.Cabinets.AsNoTracking()
            .Include(c => c.OuterDoors!).ThenInclude(d => d.InnerDoors!).ThenInclude(i => i.Authority)
            .AsSplitQuery()
            .ToDictionaryAsync(c => c.CabinetId, cancellationToken);

        // Islem istatistigi kabin basina iki gruplu sorgu: (CabinetId, StartedAtUtc) indeksi ikisini de karsilar.
        var since = DateTime.UtcNow.AddDays(-OverviewStatsDays);
        var recent = await _db.OperatorSessions.AsNoTracking()
            .Where(s => s.StartedAtUtc >= since)
            .GroupBy(s => s.CabinetId)
            .Select(g => new { CabinetId = g.Key, Count = g.Count(), Flagged = g.Count(s => s.Flags != SessionFlags.None) })
            .ToDictionaryAsync(x => x.CabinetId, cancellationToken);
        var lastStarted = await _db.OperatorSessions.AsNoTracking()
            .GroupBy(s => s.CabinetId)
            .Select(g => new { CabinetId = g.Key, StartedAtUtc = g.Max(s => s.StartedAtUtc) })
            .ToDictionaryAsync(x => x.CabinetId, x => x.StartedAtUtc, cancellationToken);

        // Sirenler tek toplu okumayla (kabin basina okuma N+1 olurdu); NO/NC cozumu cekirdekte.
        var sirenIds = configs.Values.Select(c => c.SirenIoChannelId).OfType<Guid>().Distinct().ToList();
        var sirens = sirenIds.Count == 0 ? null : await _signalizationChannelState.ReadOutputsAsync(sirenIds, cancellationToken);

        var rows = cabinets
            .OrderBy(c => c.Name)
            .Select(cabinet =>
            {
                var row = new SignalCabinetOverviewDto { CabinetId = cabinet.Id, CabinetName = cabinet.Name, CabinetIsActive = cabinet.IsActive };

                if (recent.TryGetValue(cabinet.Id, out var stats))
                    (row.SessionCount30d, row.FlaggedSessionCount30d) = (stats.Count, stats.Flagged);
                if (lastStarted.TryGetValue(cabinet.Id, out var startedAtUtc))
                    row.LastSessionStartedAtUtc = startedAtUtc;

                if (!configs.TryGetValue(cabinet.Id, out var config))
                    return row;

                var outerDoors = (config.OuterDoors ?? []).Where(d => d.IsActive).ToList();
                var innerDoors = outerDoors.SelectMany(d => d.InnerDoors ?? []).Where(i => i.IsActive).ToList();

                row.IsConfigured = true;
                row.IsEnabled = config.IsEnabled;
                row.OuterDoorCount = outerDoors.Count;
                row.InnerDoorCount = innerDoors.Count;
                row.CameraCount = outerDoors.Count(d => d.CameraId != null);
                row.AuthorityNames = [.. innerDoors.Select(i => i.Authority?.Name).OfType<string>().Distinct().Order()];
                row.HasSiren = config.SirenIoChannelId != null;
                row.SirenIsOn = config.SirenIoChannelId is Guid sirenId && sirens != null && sirens.TryGetValue(sirenId, out var siren) ? siren.IsOn : null;
                row.SirenDurationSec = config.SirenDurationSec;
                row.AwaitingCardTimeoutSec = config.AwaitingCardTimeoutSec;
                row.SessionMaxDurationMin = config.SessionMaxDurationMin;
                row.EntrySnapshotCount = config.EntrySnapshotCount;
                row.EntrySnapshotIntervalMs = config.EntrySnapshotIntervalMs;
                return row;
            })
            .ToList();

        return Result<List<SignalCabinetOverviewDto>>.Success(rows);
    }
}
