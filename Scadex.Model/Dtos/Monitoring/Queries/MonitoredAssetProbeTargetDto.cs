using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Monitoring.Queries;

/// <summary> Monitoring edilecek bir cihazın sorgu için gerekli bilgileri. </summary>
public class MonitoredAssetProbeTargetDto : IDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string IpAddress { get; set; } = null!;

    public int? MonitoringPort { get; set; }

    /// <summary> Yoklama periyodu (saniye).</summary>
    public int PingIntervalSec { get; set; }
}
