using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Automation.Queries;
using Scadex.Model.Enums;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

/// <summary>
/// Otomasyon durumunun dökümü — "otomasyon neden bu renkte". Yalnızca OKUR; katkılar <see cref="ApplyCalculatedStatusAsync"/>'in kullandığı
/// <see cref="AutomationContribution"/> kuralıyla üretilir, ön yüz kuralı kopyalamaz.
/// </summary>
public partial class AutomationStatusService
{
    /// <inheritdoc />
    public async Task<Result<AutomationStatusBreakdownDto>> GetBreakdownAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var automation = await _unitOfWork.Automations.GetAsync(
            select: c => new { c.Id, c.DeviceStatusId, c.LastSeen },
            where: c => c.Id == automationId && c.IsActive,
            cancellationToken: cancellationToken
        );
        if (automation == null)
            return Result<AutomationStatusBreakdownDto>.NotFound(message: "Otomasyon bulunamadı.");

        var card = await _unitOfWork.ControlCards.GetActiveForAutomationAsync(automationId, cancellationToken: cancellationToken);

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
            where: d => d.AutomationId == automationId && d.IsActive
                && (d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule || d.IsMonitoringEnabled || d.DeviceStatusId != null),
            cancellationToken: cancellationToken
        ) ?? [];

        var items = devices
            .Select(d => new AutomationStatusContributorDto
            {
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
                Contribution = AutomationContribution(d.DeviceStatusId, d.IsControlModule)
            })
            .OrderByDescending(i => i.Contribution is int status ? EntityEnums.DeviceStatusSeverityRank(status) : -1)
            .ThenByDescending(i => i.IsControlModule)
            .ThenBy(i => i.Name)
            .ToList();

        return Result<AutomationStatusBreakdownDto>.Success(new AutomationStatusBreakdownDto
        {
            AutomationId = automation.Id,
            StatusId = automation.DeviceStatusId,
            IsMaintenance = automation.DeviceStatusId == (int)DeviceStatus.Maintenance,
            HasControlModule = devices.Any(d => d.IsControlModule),
            LastSeen = automation.LastSeen,
            ScadaLastIngestAt = card?.ScadaLastIngestAt,
            Items = items
        });
    }
}
