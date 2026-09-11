using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Signalization.Data;
using Scadex.Signalization.Dtos.Config.Commands;
using Scadex.Signalization.Dtos.Config.Queries;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary>
/// Kabin yapilandirmasi: kabin (ortak siren) → dis kapilar (anahtar, kamera) → ic kapilar (kurum, anahtar, kilit).
/// Kapilar SANALDIR: iliski <c>Device</c>'a degil kanala (<c>IoChannel</c>) kurulur — Device bir kartin tamamidir.
/// Cekirdege referanslar FK olmadigi icin butunluk BURADA dogrulanir; DB kisitina carpip 500 uretmek yerine 400.
/// </summary>
public partial class SignalCabinetService : ISignalCabinetService
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly ISignalAuthorityService _authorityService;

    public SignalCabinetService(SignalizationDbContext db, IUnitOfWork unitOfWork, IValidationService validationService, ISignalAuthorityService authorityService)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _authorityService = authorityService;
    }

    /// <inheritdoc />
    public async Task<Result<SignalCabinetDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync(select: c => new { c.Id, c.Name }, where: c => c.Id == cabinetId, cancellationToken: cancellationToken);
        if (cabinet == null)
            return Result<SignalCabinetDto>.NotFound(message: "Kabin bulunamadı.");

        var config = await _db.Cabinets.AsNoTracking()
            .Include(c => c.OuterDoors!).ThenInclude(d => d.InnerDoors!).ThenInclude(i => i.State)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CabinetId == cabinetId, cancellationToken);

        var sirenIsOn = await _db.CabinetStates.AsNoTracking().Where(s => s.CabinetId == cabinetId).Select(s => s.SirenIsOn).FirstOrDefaultAsync(cancellationToken);

        // Hic yapilandirilmamis kabin: varsayilanlar (entity varsayilanlariyla ayni) ve bos agac.
        var source = config ?? new SignalCabinet { CabinetId = cabinetId };

        var dto = new SignalCabinetDto
        {
            CabinetId = cabinetId,
            CabinetName = cabinet.Name,
            IsConfigured = config != null,
            IsEnabled = source.IsEnabled,
            SirenIoChannelId = source.SirenIoChannelId,
            SirenDurationSec = source.SirenDurationSec,
            EntrySnapshotCount = source.EntrySnapshotCount,
            EntrySnapshotIntervalMs = source.EntrySnapshotIntervalMs,
            AwaitingCardTimeoutSec = source.AwaitingCardTimeoutSec,
            SessionMaxDurationMin = source.SessionMaxDurationMin,
            SirenIsOn = sirenIsOn,
            OuterDoors = (source.OuterDoors ?? [])
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SignalOuterDoorDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    SwitchIoChannelId = d.SwitchIoChannelId,
                    SwitchOpenValue = d.SwitchOpenValue,
                    CameraId = d.CameraId,
                    InnerDoors = (d.InnerDoors ?? [])
                        .Where(i => i.IsActive)
                        .OrderBy(i => i.Name)
                        .Select(i => new SignalInnerDoorDto
                        {
                            Id = i.Id,
                            Name = i.Name,
                            AuthorityId = i.AuthorityId,
                            SwitchIoChannelId = i.SwitchIoChannelId,
                            SwitchOpenValue = i.SwitchOpenValue,
                            LockIoChannelId = i.LockIoChannelId,
                            UnlockTurnsOn = i.UnlockTurnsOn,
                            IsUnlocked = i.State?.IsUnlocked == true
                        })
                        .ToList()
                })
                .ToList()
        };

        return Result<SignalCabinetDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<Result<SignalCabinetOptionsDto>> GetOptionsAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        bool cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == cabinetId, cancellationToken: cancellationToken);
        if (!cabinetExists)
            return Result<SignalCabinetOptionsDto>.NotFound(message: "Kabin bulunamadı.");

        // Silinmis (pasif) kartin kanallari yerinde kalir (§5.2) ama secenek olarak sunulmaz.
        var channels = await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new { c.Id, c.Direction, c.ChannelNumber, c.Name, c.CurrentValue, c.DeviceId, DeviceName = c.Device!.Name, DeviceIsActive = c.Device.IsActive },
            where: c => c.CabinetId == cabinetId && c.IsEnabled,
            cancellationToken: cancellationToken) ?? [];
        channels = channels.Where(c => c.DeviceIsActive).ToList();

        var wiredNames = await LoadWiredDeviceNamesAsync(channels.Select(c => (c.Id, c.DeviceId)).ToList(), cancellationToken);
        var usedBy = await LoadChannelUsageAsync(cabinetId, cancellationToken);

        SignalChannelOptionDto ToOption(Guid id, PinDirection direction, int number, string name, string? currentValue, string deviceName) => new()
        {
            Id = id,
            ChannelNumber = number,
            Address = ScadaPinAddress.Format(direction, number),
            ChannelName = name,
            DeviceName = deviceName,
            WiredDeviceName = wiredNames.GetValueOrDefault(id),
            CurrentValue = currentValue,
            UsedBy = usedBy.GetValueOrDefault(id)
        };

        var cameras = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new SignalCameraOptionDto { Id = c.Id, Name = c.Name, IsActive = c.IsActive },
            where: c => c.CabinetId == cabinetId,
            cancellationToken: cancellationToken) ?? [];

        var authorities = await _authorityService.GetAllAsync(cancellationToken);

        var dto = new SignalCabinetOptionsDto
        {
            InputChannels = channels
                .Where(c => c.Direction == PinDirection.Input)
                .OrderBy(c => c.DeviceName).ThenBy(c => c.ChannelNumber)
                .Select(c => ToOption(c.Id, c.Direction, c.ChannelNumber, c.Name, c.CurrentValue, c.DeviceName))
                .ToList(),
            OutputChannels = channels
                .Where(c => c.Direction == PinDirection.Output)
                .OrderBy(c => c.DeviceName).ThenBy(c => c.ChannelNumber)
                .Select(c => ToOption(c.Id, c.Direction, c.ChannelNumber, c.Name, c.CurrentValue, c.DeviceName))
                .ToList(),
            Cameras = cameras.OrderBy(c => c.Name).ToList(),
            Authorities = authorities.IsSuccess ? authorities.Data.Where(a => a.IsActive).ToList() : []
        };

        return Result<SignalCabinetOptionsDto>.Success(dto);
    }

    /// <summary>
    /// Kanalin pinine TEK ADIMLIK kabloyla bagli, kanalin kendi kartindan farkli cihazlarin adlari (etiket).
    /// Klemens uzerinden cok adimli izleme bilerek yapilmaz — kirilgan olurdu, anlami tasiyan kanal adresidir.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadWiredDeviceNamesAsync(List<(Guid ChannelId, Guid CardDeviceId)> channels, CancellationToken cancellationToken)
    {
        var channelIds = channels.Select(c => c.ChannelId).ToList();
        if (channelIds.Count == 0)
            return [];

        var channelPins = await _unitOfWork.Pins.GetAllAsync(
            select: p => new { p.Id, ChannelId = p.IoChannelId!.Value },
            where: p => p.IoChannelId != null && channelIds.Contains(p.IoChannelId.Value),
            cancellationToken: cancellationToken) ?? [];
        if (channelPins.Count == 0)
            return [];

        var pinIds = channelPins.Select(p => p.Id).ToList();
        var connections = await _unitOfWork.Connections.GetAllAsync(
            select: c => new { c.SourcePinId, c.TargetPinId },
            where: c => pinIds.Contains(c.SourcePinId) || pinIds.Contains(c.TargetPinId),
            cancellationToken: cancellationToken) ?? [];

        var channelByPin = channelPins.ToDictionary(p => p.Id, p => p.ChannelId);
        var links = new List<(Guid ChannelId, Guid OtherPinId)>();
        foreach (var connection in connections)
        {
            if (channelByPin.TryGetValue(connection.SourcePinId, out var fromSource)) links.Add((fromSource, connection.TargetPinId));
            if (channelByPin.TryGetValue(connection.TargetPinId, out var fromTarget)) links.Add((fromTarget, connection.SourcePinId));
        }
        if (links.Count == 0)
            return [];

        var otherPinIds = links.Select(l => l.OtherPinId).Distinct().ToList();
        var otherPins = await _unitOfWork.Pins.GetAllAsync(
            select: p => new { p.Id, p.DeviceId, DeviceName = p.Device!.Name },
            where: p => otherPinIds.Contains(p.Id),
            cancellationToken: cancellationToken) ?? [];
        var otherById = otherPins.ToDictionary(p => p.Id);
        var cardByChannel = channels.ToDictionary(c => c.ChannelId, c => c.CardDeviceId);

        return links
            .Where(l => otherById.ContainsKey(l.OtherPinId) && otherById[l.OtherPinId].DeviceId != cardByChannel[l.ChannelId])
            .GroupBy(l => l.ChannelId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(l => otherById[l.OtherPinId].DeviceName).Distinct()));
    }

    /// <summary> Kanal → onu kullanan kapinin adi (mevcut, aktif yapilandirmaya gore). </summary>
    private async Task<Dictionary<Guid, string>> LoadChannelUsageAsync(Guid cabinetId, CancellationToken cancellationToken)
    {
        var usage = new Dictionary<Guid, string>();

        var siren = await _db.Cabinets.AsNoTracking().Where(c => c.CabinetId == cabinetId).Select(c => c.SirenIoChannelId).FirstOrDefaultAsync(cancellationToken);
        if (siren is Guid sirenId)
            usage[sirenId] = "Kabin sireni";

        var outerDoors = await _db.OuterDoors.AsNoTracking().Where(d => d.CabinetId == cabinetId && d.IsActive).Select(d => new { d.SwitchIoChannelId, d.Name }).ToListAsync(cancellationToken);
        foreach (var d in outerDoors)
            usage[d.SwitchIoChannelId] = d.Name;

        var innerDoors = await _db.InnerDoors.AsNoTracking()
            .Where(i => i.IsActive && i.OuterDoor!.IsActive && i.OuterDoor.CabinetId == cabinetId)
            .Select(i => new { i.SwitchIoChannelId, i.LockIoChannelId, i.Name })
            .ToListAsync(cancellationToken);
        foreach (var i in innerDoors)
        {
            usage[i.SwitchIoChannelId] = $"{i.Name} (anahtar)";
            usage[i.LockIoChannelId] = $"{i.Name} (kilit)";
        }

        return usage;
    }
}
