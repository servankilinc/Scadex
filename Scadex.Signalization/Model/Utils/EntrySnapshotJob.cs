namespace Scadex.Signalization.Model.Utils;

/// <summary> <paramref name="InnerDoorId"/> yalnızca kart okutma kaynaklı çekimde doludur. </summary>
public sealed class EntrySnapshotJob
{
    public EntrySnapshotJob(long sessionId, Guid cameraId, int count, int intervalMs, DateTime startAtUtc, Guid? innerDoorId = null)
    {
        SessionId = sessionId;
        CameraId = cameraId;
        Count = count;
        IntervalMs = intervalMs;
        StartAtUtc = startAtUtc;
        InnerDoorId = innerDoorId;
    }

    public long SessionId { get; set; }
    public Guid CameraId { get; set; }
    public int Count { get; set; }
    public int IntervalMs { get; set; }
    public DateTime StartAtUtc { get; set; }
    public Guid? InnerDoorId { get; set; }
}
