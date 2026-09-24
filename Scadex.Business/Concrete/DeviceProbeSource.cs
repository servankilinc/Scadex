using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Monitoring.Queries;

namespace Scadex.Business.Concrete;

/// <summary> Monitoring'i açık cihazların (SCADA kartı, POS, PC vs.) haberleşme kontrolünü gerçekleştirir </summary>
public class DeviceProbeSource : IMonitoredAssetProbeSource
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICabinetStatusService _cabinetStatusService;

    public DeviceProbeSource(IUnitOfWork unitOfWork, ICabinetStatusService cabinetStatusService)
    {
        _unitOfWork = unitOfWork;
        _cabinetStatusService = cabinetStatusService;
    }

    /// <inheritdoc/>
    public string AssetTypeName => "Device";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonitoredAssetProbeTargetDto>> GetProbeTargetsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new MonitoredAssetProbeTargetDto
            {
                Id = d.Id,
                Name = d.Name,
                IpAddress = d.IpAddress!,
                MonitoringPort = d.MonitoringPort,
                PingIntervalSec = d.PingIntervalSec
            },
            // ComponentTemplate izlenebilir degilse cihazdaki bool alan yok say
            where: d => d.IsActive && d.IsMonitoringEnabled && d.ComponentTemplate!.IsMonitorable && d.IpAddress != null && d.IpAddress != "" && d.Cabinet!.IsActive,
            cancellationToken: cancellationToken
        ) ?? [];

        return [.. rows];
    }

    /// <inheritdoc/>
    public Task<Result> RecordProbeResultAsync(Guid assetId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default) =>
        _cabinetStatusService.RecordDeviceProbeResultAsync(assetId, result, cancellationToken);
}
