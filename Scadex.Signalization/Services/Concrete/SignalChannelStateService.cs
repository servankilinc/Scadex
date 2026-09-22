using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.DataAccess.UoW;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Services.Concrete;

/// <inheritdoc />
/// <remarks>
/// Sinyalizasyon modülündeki kapı switch gibi modül'e ait cihazların Scadex çekirdeğinde tutulan Kanal bilgiler ışığında durumularının çözülmesini sağlar
/// </remarks>
public class SignalChannelStateService : ISignalChannelStateService
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIoChannelService _ioChannelService;
    public SignalChannelStateService(SignalizationDbContext db, IUnitOfWork unitOfWork, IIoChannelService ioChannelService)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ioChannelService = ioChannelService;
    }

    #region I/O Çözümlemleri

    /// <inheritdoc />
    public async Task<bool> AreAllInnerDoorsLockedAsync(Guid outerDoorId, CancellationToken cancellationToken = default)
    {
        var doors = await _db.InnerDoors.AsNoTracking()
            .Where(i => i.OuterDoorId == outerDoorId && i.IsActive)
            .ToListAsync(cancellationToken);

        if (doors.Count == 0)
            return true;

        // 1) Kilidi açık görünen kapı varsa biri hâlâ çalışıyor
        var locks = await ReadLocksAsync(doors, cancellationToken);
        if (locks.Values.Any(l => l.IsOn == true))
            return false;

        // 2) Kilit "hepsi kilitli" diyor; sahaya da sormadan siren çaldırmayız / oturumu kapatmayız.
        var switches = await ReadSwitchesAsync(doors.Select(d => (d.Id, d.SwitchIoChannelId, d.SwitchOpenValue)).ToList(), cancellationToken);
        return !switches.Values.Any(s => s.IsOn == true);
    }

    /// <inheritdoc />
    public Task<ChannelReading> GetSwitchStateAsync(SignalOuterDoor outerDoor, CancellationToken cancellationToken = default) =>
        ReadSwitchAsync(outerDoor.SwitchIoChannelId, outerDoor.SwitchOpenValue, cancellationToken);

    /// <inheritdoc />
    public Task<ChannelReading> GetSwitchStateAsync(SignalInnerDoor innerDoor, CancellationToken cancellationToken = default) =>
        ReadSwitchAsync(innerDoor.SwitchIoChannelId, innerDoor.SwitchOpenValue, cancellationToken);

    /// <inheritdoc />
    public Task<ChannelReading> GetLightStateAsync(SignalOuterDoor outerDoor, CancellationToken cancellationToken = default) =>
        outerDoor.LightIoChannelId is Guid lightChannelId ? ReadOutputAsync(lightChannelId, cancellationToken) : Task.FromResult(ChannelReading.Unknown);

    /// <inheritdoc />
    public Task<ChannelReading> GetSirenStateAsync(SignalCabinet cabinet, CancellationToken cancellationToken = default) =>
        cabinet.SirenIoChannelId is Guid sirenChannelId ? ReadOutputAsync(sirenChannelId, cancellationToken) : Task.FromResult(ChannelReading.Unknown);

    /// <inheritdoc />
    public async Task<ChannelReading> GetLockStateAsync(SignalInnerDoor innerDoor, CancellationToken cancellationToken = default)
    {
        var locks = await ReadLocksAsync([innerDoor], cancellationToken);
        return locks[innerDoor.Id];
    }
    #endregion

    #region Toplu okuma
    /// <inheritdoc />
    public async Task<SignalCabinetReadings> ReadCabinetAsync(SignalCabinet cabinet, CancellationToken cancellationToken = default)
    {
        var outerDoors = (cabinet.OuterDoors ?? []).Where(d => d.IsActive).ToList();
        var innerDoors = outerDoors.SelectMany(d => (d.InnerDoors ?? []).Where(i => i.IsActive)).ToList();

        // Bütün anahtarlar tek sorguda
        var switches = await ReadSwitchesAsync(
            outerDoors.Select(d => (d.Id, d.SwitchIoChannelId, d.SwitchOpenValue))
                .Concat(innerDoors.Select(i => (i.Id, i.SwitchIoChannelId, i.SwitchOpenValue)))
                .ToList(),
            cancellationToken
        );

        // Bütün çıkışlar (siren, aydınlatmalar, kilitler) tek çağrıda
        var outputChannelIds = new List<Guid>();
        if (cabinet.SirenIoChannelId is Guid sirenChannelId)
            outputChannelIds.Add(sirenChannelId);
        outputChannelIds.AddRange(outerDoors.Select(d => d.LightIoChannelId).OfType<Guid>());
        outputChannelIds.AddRange(innerDoors.Select(i => i.LockIoChannelId));

        var outputs = await ReadOutputsAsync(outputChannelIds, cancellationToken);

        return new SignalCabinetReadings(
            Siren: cabinet.SirenIoChannelId is Guid siren ? outputs[siren] : ChannelReading.Unknown,
            OuterDoorSwitches: outerDoors.ToDictionary(d => d.Id, d => switches[d.Id]),
            OuterDoorLights: outerDoors.ToDictionary(d => d.Id, d => d.LightIoChannelId is Guid light ? outputs[light] : ChannelReading.Unknown),
            InnerDoorSwitches: innerDoors.ToDictionary(i => i.Id, i => switches[i.Id]),
            InnerDoorLocks: innerDoors.ToDictionary(i => i.Id, i => ToLockReading(outputs[i.LockIoChannelId], i.UnlockTurnsOn))
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ChannelReading>> ReadOutputsAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default)
    {
        var states = await _ioChannelService.GetOutputStatesAsync(ioChannelIds, cancellationToken);
        return states.ToDictionary(s => s.Key, s => new ChannelReading(s.Value.IsOn, s.Value.IsOn == null ? null : s.Value.ValueUpdatedAt));
    }
    #endregion

    #region Helpers
    private async Task<ChannelReading> ReadOutputAsync(Guid ioChannelId, CancellationToken cancellationToken)
    {
        var outputs = await ReadOutputsAsync([ioChannelId], cancellationToken);
        return outputs[ioChannelId];
    }

    private async Task<ChannelReading> ReadSwitchAsync(Guid switchIoChannelId, string openValue, CancellationToken cancellationToken)
    {
        var switches = await ReadSwitchesAsync([(switchIoChannelId, switchIoChannelId, openValue)], cancellationToken);
        return switches[switchIoChannelId];
    }

    /// <summary> Anahtarların okunuşu; sözlük <c>Key</c> ile (kapı kimliği) anahtarlanır. Pasif/okunamamış kanal "bilinmiyor". </summary>
    private async Task<Dictionary<Guid, ChannelReading>> ReadSwitchesAsync(IReadOnlyCollection<(Guid Key, Guid ChannelId, string OpenValue)> switches, CancellationToken cancellationToken)
    {
        if (switches.Count == 0)
            return [];

        var channelIds = switches.Select(s => s.ChannelId).Distinct().ToList();
        var channels = (await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new { c.Id, c.CurrentValue, c.ValueUpdatedAt },
            where: c => channelIds.Contains(c.Id) && c.IsEnabled,
            cancellationToken: cancellationToken
        ) ?? []).ToDictionary(c => c.Id);

        var result = new Dictionary<Guid, ChannelReading>(switches.Count);
        foreach (var (key, channelId, openValue) in switches)
        {
            result[key] = channels.TryGetValue(channelId, out var channel) && channel.CurrentValue != null
                ? new ChannelReading(string.Equals(channel.CurrentValue, openValue, StringComparison.Ordinal), channel.ValueUpdatedAt)
                : ChannelReading.Unknown;
        }

        return result;
    }

    /// <summary> İç kapı kilitlerinin okunuşu; sözlük kapı kimliğiyle anahtarlanır. </summary>
    private async Task<Dictionary<Guid, ChannelReading>> ReadLocksAsync(IReadOnlyCollection<SignalInnerDoor> doors, CancellationToken cancellationToken)
    {
        var outputs = await ReadOutputsAsync(doors.Select(d => d.LockIoChannelId).ToList(), cancellationToken);
        return doors.ToDictionary(d => d.Id, d => ToLockReading(outputs[d.LockIoChannelId], d.UnlockTurnsOn));
    }

    /// <summary> Kilit rölesinin mantıksal durumu → "kilit açık mı": kilidi açmak için hangi değerin gittiği kilidin kendi mantığıdır. </summary>
    private static ChannelReading ToLockReading(ChannelReading relay, bool unlockTurnsOn) =>
        relay.IsOn is bool relayOn ? new ChannelReading(relayOn == unlockTurnsOn, relay.ChangedAtUtc) : ChannelReading.Unknown;
    #endregion
}
