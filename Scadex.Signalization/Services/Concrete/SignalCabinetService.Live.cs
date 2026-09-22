using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Model.Dtos.Config.Queries;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Sinyalizasyon kabini okuma yolu kapi ve outputların(kilit, siren, aydınlatma...) o anki durumunu da verir. </summary>
public partial class SignalCabinetService
{
    /// <inheritdoc />
    public async Task<Result<SignalCabinetLiveDto>> GetLiveAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync(select: c => new { c.Id, c.Name }, where: c => c.Id == cabinetId, cancellationToken: cancellationToken);
        if (cabinet == null)
            return Result<SignalCabinetLiveDto>.NotFound(message: "Kabin bulunamadı.");

        var config = await _db.Cabinets.AsNoTracking()
            .Include(c => c.OuterDoors!).ThenInclude(d => d.InnerDoors!).ThenInclude(i => i.Authority)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CabinetId == cabinetId, cancellationToken);

        // Hic yapilandirilmamis kabin: varsayilanlar ve bos agac (GetAsync ile ayni davranis).
        var source = config ?? new SignalCabinet { CabinetId = cabinetId };
        var outerDoors = (source.OuterDoors ?? []).Where(d => d.IsActive).OrderBy(d => d.Name).ToList();

        // Butun kapi ve cikis durumlari toplu okunur: kapi basina ayri okuma N+1 olurdu.
        var readings = await _signalizationChannelState.ReadCabinetAsync(source, cancellationToken);
        var cameraNames = await LoadCameraNamesAsync(outerDoors.Select(d => d.CameraId).OfType<Guid>().Distinct().ToList(), cancellationToken);

        var dto = new SignalCabinetLiveDto
        {
            CabinetId = cabinetId,
            CabinetName = cabinet.Name,
            IsConfigured = config != null,
            IsEnabled = source.IsEnabled,
            SirenIoChannelId = source.SirenIoChannelId,
            SirenIsOn = readings.Siren.IsOn,
            SirenChangedAtUtc = readings.Siren.ChangedAtUtc,
            OuterDoors = outerDoors.Select(outer =>
            {
                var outerSwitch = readings.OuterDoorSwitches.GetValueOrDefault(outer.Id, ChannelReading.Unknown);
                var light = readings.OuterDoorLights.GetValueOrDefault(outer.Id, ChannelReading.Unknown);

                return new SignalOuterDoorLiveDto
                {
                    Id = outer.Id,
                    Name = outer.Name,
                    CameraId = outer.CameraId,
                    CameraName = outer.CameraId is Guid cameraId && cameraNames.TryGetValue(cameraId, out var cameraName) ? cameraName : null,
                    SwitchIoChannelId = outer.SwitchIoChannelId,
                    SwitchOpenValue = outer.SwitchOpenValue,
                    IsOpen = outerSwitch.IsOn,
                    SwitchChangedAtUtc = outerSwitch.ChangedAtUtc,
                    LightIoChannelId = outer.LightIoChannelId,
                    LightIsOn = light.IsOn,
                    LightChangedAtUtc = light.ChangedAtUtc,
                    InnerDoors = (outer.InnerDoors ?? [])
                        .Where(i => i.IsActive)
                        .OrderBy(i => i.Name)
                        .Select(inner =>
                        {
                            var innerSwitch = readings.InnerDoorSwitches.GetValueOrDefault(inner.Id, ChannelReading.Unknown);
                            var lockState = readings.InnerDoorLocks.GetValueOrDefault(inner.Id, ChannelReading.Unknown);

                            return new SignalInnerDoorLiveDto
                            {
                                Id = inner.Id,
                                Name = inner.Name,
                                AuthorityId = inner.AuthorityId,
                                AuthorityName = inner.Authority?.Name,
                                SwitchIoChannelId = inner.SwitchIoChannelId,
                                SwitchOpenValue = inner.SwitchOpenValue,
                                IsOpen = innerSwitch.IsOn,
                                SwitchChangedAtUtc = innerSwitch.ChangedAtUtc,
                                LockIoChannelId = inner.LockIoChannelId,
                                IsUnlocked = lockState.IsOn,
                                LockChangedAtUtc = lockState.ChangedAtUtc
                            };
                        })
                        .ToList()
                };
            }).ToList()
        };

        return Result<SignalCabinetLiveDto>.Success(dto);
    }

    #region Helpers

    private async Task<Dictionary<Guid, string>> LoadCameraNamesAsync(List<Guid> cameraIds, CancellationToken cancellationToken)
    {
        if (cameraIds.Count == 0)
            return [];

        var cameras = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.Id, c.Name },
            where: c => cameraIds.Contains(c.Id),
            cancellationToken: cancellationToken
        ) ?? [];

        return cameras.ToDictionary(c => c.Id, c => c.Name);
    }

    #endregion
}
