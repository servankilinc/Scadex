using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Model.Dtos.Session.Queries;
using Scadex.Signalization.Services.Abstract;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Operator islemlerinin okuma yuzu. Oturumlari yalnizca motor yazar; bu servis hicbir satir yazmaz. </summary>
public partial class OperatorSessionService : IOperatorSessionService
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly ISignalChannelStateService _signalizationChannelState;

    public OperatorSessionService(SignalizationDbContext db, IUnitOfWork unitOfWork, IValidationService validationService, ISignalChannelStateService signalizationChannelState)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _signalizationChannelState = signalizationChannelState;
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<OperatorSessionOpenDto>>> GetOpenAsync(Guid? cabinetId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = _db.OperatorSessions.AsNoTracking().Where(s => s.EndedAtUtc == null);

        if (cabinetId is Guid cid)
            query = query.Where(s => s.CabinetId == cid);

        var rows = await query
            .OrderByDescending(s => s.StartedAtUtc)
            .Select(s => new OperatorSessionOpenDto
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
                SirenRequested = s.SirenRequestedAtUtc != null && s.SirenReleasedAtUtc == null,
                Operators = s.Operators!.OrderBy(o => o.FirstCardAtUtc).Select(o => new OperatorSessionOperatorDto
                {
                    UserId = o.UserId,
                    FullName = o.FullNameSnapshot,
                    AuthorityName = o.AuthorityNameSnapshot,
                    CardIdRaw = o.CardIdRaw,
                    FirstCardAtUtc = o.FirstCardAtUtc,
                    LastCardAtUtc = o.LastCardAtUtc
                }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Result<ICollection<OperatorSessionOpenDto>>.Success(rows);

        // Asama TURETILIR: kilitsiz ic kapi var mi (dis kapinin ardinda)? Kilit ve siren durumu kanaldan TEK cagrida okunur.
        var outerIds = rows.Select(r => r.OuterDoorId).Distinct().ToList();
        var innerDoors = await _db.InnerDoors.AsNoTracking()
            .Where(i => outerIds.Contains(i.OuterDoorId) && i.IsActive)
            .Select(i => new { i.OuterDoorId, i.LockIoChannelId, i.UnlockTurnsOn })
            .ToListAsync(cancellationToken);

        var cabinetIds = rows.Select(r => r.CabinetId).Distinct().ToList();
        var sirens = await _db.Cabinets.AsNoTracking()
            .Where(c => cabinetIds.Contains(c.CabinetId) && c.SirenIoChannelId != null)
            .Select(c => new { c.CabinetId, SirenIoChannelId = c.SirenIoChannelId!.Value })
            .ToListAsync(cancellationToken);

        var outputs = await _signalizationChannelState.ReadOutputsAsync(
            innerDoors.Select(i => i.LockIoChannelId).Concat(sirens.Select(s => s.SirenIoChannelId)).ToList(),
            cancellationToken
        );

        // Kilit rolesinin mantiksal durumu "kilidi acan" degere esitse kilit aciktir.
        var unlockedOuterIds = innerDoors
            .Where(i => outputs[i.LockIoChannelId].IsOn is bool relayOn && relayOn == i.UnlockTurnsOn)
            .Select(i => i.OuterDoorId)
            .ToHashSet();

        var sirenOn = sirens
            .Where(s => outputs[s.SirenIoChannelId].IsOn == true)
            .Select(s => s.CabinetId)
            .ToHashSet();
        var cabinetNames = await LoadCabinetNamesAsync(cabinetIds, cancellationToken);

        foreach (var row in rows)
        {
            row.CabinetName = cabinetNames.GetValueOrDefault(row.CabinetId);
            row.CabinetSirenIsOn = sirenOn.Contains(row.CabinetId);
            row.ElapsedSec = Math.Max(0, (int)(now - row.StartedAtUtc).TotalSeconds);
            row.Phase = row.Operators.Count == 0 ? SessionPhase.AwaitingCard
                : unlockedOuterIds.Contains(row.OuterDoorId) ? SessionPhase.Inside
                : SessionPhase.Exiting;
        }

        return Result<ICollection<OperatorSessionOpenDto>>.Success(rows);
    }

    private async Task<Dictionary<Guid, string>> LoadCabinetNamesAsync(IEnumerable<Guid> cabinetIds, CancellationToken cancellationToken)
    {
        var ids = cabinetIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var cabinets = await _unitOfWork.Cabinets.GetAllAsync(
            select: c => new { c.Id, c.Name },
            where: c => ids.Contains(c.Id),
            cancellationToken: cancellationToken) ?? [];

        return cabinets.ToDictionary(c => c.Id, c => c.Name);
    }

    private async Task<Dictionary<Guid, string>> LoadUserNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var users = await _unitOfWork.Users.GetAllAsync(
            select: u => new { u.Id, u.FullName },
            where: u => ids.Contains(u.Id),
            cancellationToken: cancellationToken) ?? [];

        return users.ToDictionary(u => u.Id, u => u.FullName);
    }
}
