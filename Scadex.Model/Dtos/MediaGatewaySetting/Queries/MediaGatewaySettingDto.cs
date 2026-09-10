using Scadex.Core.Model;

namespace Scadex.Model.Dtos.MediaGatewaySetting.Queries;

public class MediaGatewaySettingDto : IDto
{
    public int ApiTimeoutMs { get; set; }
    public string ApiBaseUrl { get; set; } = null!;
    public string WebRtcPublicBaseUrl { get; set; } = null!;
    public int TokenTtlSeconds { get; set; }

    /// <summary>Saniye. Kolonda <c>"10s"</c> olarak durur; istemci biçim görmez.</summary>
    public int SourceOnDemandCloseAfterSec { get; set; }

    public string RtspTransport { get; set; } = null!;
    public string RecordRoot { get; set; } = null!;
}
