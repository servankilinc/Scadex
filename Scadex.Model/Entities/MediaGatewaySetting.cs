using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class MediaGatewaySetting : IEntity, IAuditableEntity
{
    public int Id { get; set; }

    public const int SingleRowId = 1;

    public int ApiTimeoutMs { get; set; }
    public string ApiBaseUrl { get; set; } = null!;
    public string WebRtcPublicBaseUrl { get; set; } = null!;
    public int TokenTtlSeconds { get; set; }
    public string SourceOnDemandCloseAfter { get; set; } = null!;
    public string RtspTransport { get; set; } = null!;
    public string RecordRoot { get; set; } = null!;

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion
}
