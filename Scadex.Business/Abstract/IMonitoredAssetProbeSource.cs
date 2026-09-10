using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Monitoring.Queries;

namespace Scadex.Business.Abstract;

/// <summary> <see cref="Model.Entities.Abstract.IMonitoredAsset"/> entitylerini sağlayan kaynak </summary>
public interface IMonitoredAssetProbeSource
{
    /// <summary>Log satirlarinda hangi tipin yoklandigini soylemek icin — orn. "Camera".</summary>
    string AssetTypeName { get; }

    /// <summary>Monitoring edilecek bir cihazın sorgu için gerekli bilgileri sağlar.</summary> 
    Task<IReadOnlyList<MonitoredAssetProbeTargetDto>> GetProbeTargetsAsync(CancellationToken cancellationToken = default);

    /// <summary> Yoklama sonucunu ilgili entitye yazar. </summary>
    Task<Result> RecordProbeResultAsync(Guid assetId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default);
}
