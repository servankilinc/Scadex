using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile;

public class HikvisionProtocolProfile : ICameraProtocolProfile
{
    public string Manufacturer => "Hikvision";

    /// <inheritdoc/>
    public string BuildRtspUrl(Camera camera, StreamProfile profile)
    {
        int channel = profile == StreamProfile.Main ? camera.MainStreamChannel : camera.SubStreamChannel;
        return $"rtsp://{camera.Username}:{camera.Password}@{camera.IpAddress}:{camera.RtspPort}/Streaming/Channels/{channel}";
    }

    /// <inheritdoc/>
    public string BuildSnapshotPath(Camera camera) => $"/ISAPI/Streaming/channels/{camera.SnapshotChannel}/picture";
}
