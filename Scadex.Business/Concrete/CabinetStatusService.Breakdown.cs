using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Enums;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

/// <summary>
/// Kabin durumunun dökümü — "kabin neden bu renkte". Yalnızca OKUR; katkılar <see cref="ApplyCalculatedStatusAsync"/>'in kullandığı
/// <see cref="CabinetContribution"/> / <see cref="CameraContribution"/> kurallarıyla üretilir, ön yüz kuralı kopyalamaz.
/// </summary>
public partial class CabinetStatusService
{
    /// <inheritdoc />
    public async Task<Result<CabinetStatusBreakdownDto>> GetBreakdownAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync(
            select: c => new { c.Id, c.DeviceStatusId, c.LastSeen, c.ScadaLastIngestAt },
            where: c => c.Id == cabinetId && c.IsActive,
            cancellationToken: cancellationToken
        );
        if (cabinet == null)
            return Result<CabinetStatusBreakdownDto>.NotFound(message: "Kabin bulunamadı.");

        // Hesaplamanın gördüğü kümenin TAMAMI: durumu dolu her aktif cihaz katkı verir; durumu henüz boş olsa da kontrol modülü
        // ve izlenen cihaz listelenir — "neden Bilinmiyor" sorusunun cevabı onlardır.
        var devices = await _unitOfWork.Devices.GetAllAsync(
            select: d => new
            {
                d.Id,
                d.Name,
                TemplateName = d.ComponentTemplate!.Name,
                IsControlModule = d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule,
                d.IsMonitoringEnabled,
                d.IpAddress,
                d.MonitoringPort,
                d.PingIntervalSec,
                d.DeviceStatusId,
                d.LastSeen,
                d.LastConnectionError
            },
            where: d => d.CabinetId == cabinetId && d.IsActive
                && (d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule || d.IsMonitoringEnabled || d.DeviceStatusId != null),
            cancellationToken: cancellationToken
        ) ?? [];

        // Yalnızca izlenen kamera katkı verir (ApplyCalculatedStatusAsync ile aynı küme).
        var cameras = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.Id, c.Name, c.IpAddress, c.MonitoringPort, c.PingIntervalSec, c.DeviceStatusId, c.LastSeen, c.LastConnectionError },
            where: c => c.CabinetId == cabinetId && c.IsActive && c.IsMonitoringEnabled,
            cancellationToken: cancellationToken
        ) ?? [];

        var items = devices
            .Select(d => new CabinetStatusContributorDto
            {
                Kind = EntityEnums.CabinetStatusContributorKind.Device,
                Id = d.Id,
                Name = d.Name,
                TemplateName = d.TemplateName,
                IsControlModule = d.IsControlModule,
                IsMonitoringEnabled = d.IsMonitoringEnabled,
                IpAddress = d.IpAddress,
                MonitoringPort = d.MonitoringPort,
                PingIntervalSec = d.PingIntervalSec,
                StatusId = d.DeviceStatusId,
                LastSeen = d.LastSeen,
                LastConnectionError = d.LastConnectionError,
                Contribution = CabinetContribution(d.DeviceStatusId, d.IsControlModule)
            })
            .Concat(cameras.Select(c => new CabinetStatusContributorDto
            {
                Kind = EntityEnums.CabinetStatusContributorKind.Camera,
                Id = c.Id,
                Name = c.Name,
                IsMonitoringEnabled = true,
                IpAddress = c.IpAddress,
                MonitoringPort = c.MonitoringPort,
                PingIntervalSec = c.PingIntervalSec,
                StatusId = c.DeviceStatusId,
                LastSeen = c.LastSeen,
                LastConnectionError = c.LastConnectionError,
                Contribution = CameraContribution(c.DeviceStatusId)
            }))
            .OrderByDescending(i => i.Contribution is int status ? EntityEnums.DeviceStatusSeverityRank(status) : -1)
            .ThenByDescending(i => i.IsControlModule)
            .ThenBy(i => i.Name)
            .ToList();

        return Result<CabinetStatusBreakdownDto>.Success(new CabinetStatusBreakdownDto
        {
            CabinetId = cabinet.Id,
            StatusId = cabinet.DeviceStatusId,
            IsMaintenance = cabinet.DeviceStatusId == (int)DeviceStatus.Maintenance,
            HasControlModule = devices.Any(d => d.IsControlModule),
            LastSeen = cabinet.LastSeen,
            ScadaLastIngestAt = cabinet.ScadaLastIngestAt,
            Items = items
        });
    }
}
