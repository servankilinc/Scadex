using Scadex.Core.Model;

namespace Scadex.Model.Entities;

public class CameraCaptureSetting : IEntity, IAuditableEntity
{
    public int Id { get; set; }

    public const int SingleRowId = 1;

    public int SnapshotTimeoutMs { get; set; }
    public int SnapshotCacheSeconds { get; set; }
    public string CaptureRoot { get; set; } = null!;
    public int CaptureRetentionDays { get; set; }
    public int MaxClipDurationSec { get; set; }
    public int ClipFinalizeGraceMs { get; set; }

    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    #endregion
}
