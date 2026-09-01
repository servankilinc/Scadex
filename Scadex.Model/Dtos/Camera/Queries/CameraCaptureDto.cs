using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Queries;

public class CameraCaptureDto : IDto
{
    public long Id { get; set; }
    public Guid CameraId { get; set; }
    public CaptureType Type { get; set; }
    public CaptureStatus Status { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public int? DurationSec { get; set; }
    public string? RelativePath { get; set; }
    public long? SizeBytes { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RequestedByUserId { get; set; }
}
