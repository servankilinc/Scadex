using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Monitoring.Queries;

namespace Scadex.Business.Concrete;

public class CameraProbeSource : IMonitoredAssetProbeSource
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICameraService _cameraService;

    public CameraProbeSource(IUnitOfWork unitOfWork, ICameraService cameraService)
    {
        _unitOfWork = unitOfWork;
        _cameraService = cameraService;
    }

    /// <inheritdoc/>
    public string AssetTypeName => "Camera";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonitoredAssetProbeTargetDto>> GetProbeTargetsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new MonitoredAssetProbeTargetDto
            {
                Id = c.Id,
                Name = c.Name,
                IpAddress = c.IpAddress,
                MonitoringPort = c.MonitoringPort,
                PingIntervalSec = c.PingIntervalSec
            },
            where: c => c.IsActive && c.IsMonitoringEnabled,
            cancellationToken: cancellationToken) ?? [];

        return [.. rows];
    }

    /// <inheritdoc/>
    public Task<Result> RecordProbeResultAsync(Guid assetId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default) =>
        _cameraService.RecordProbeResultAsync(assetId, result, cancellationToken);
}
