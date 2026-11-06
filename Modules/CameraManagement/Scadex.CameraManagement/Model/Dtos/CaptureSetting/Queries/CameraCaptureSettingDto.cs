using Scadex.Core.Model;

namespace Scadex.CameraManagement.Model.Dtos.CaptureSetting.Queries;

public class CameraCaptureSettingDto : IDto
{
    public int SnapshotTimeoutMs { get; set; }
    public int SnapshotCacheSeconds { get; set; }
    public string CaptureRoot { get; set; } = null!;
    public int CaptureRetentionDays { get; set; }
    public int MaxClipDurationSec { get; set; }
    public int ClipFinalizeGraceMs { get; set; }
}
