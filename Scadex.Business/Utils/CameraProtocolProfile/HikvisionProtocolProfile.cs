using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile;

public class HikvisionProtocolProfile : ICameraProtocolProfile
{
    /// <summary> Hikvision kanal kodlamasi: kamera kanali x 100 + akim no. Kameraya dogrudan baglaniliyor (NVR degil), kanal hep 1. </summary>
    private const int MainChannel = 101;
    private const int SubChannel = 102;

    public CameraBrand Brand => CameraBrand.Hikvision;

    public int RtspPort => 554;

    public bool HasSubStream => true;

    /// <inheritdoc/>
    public string BuildRtspUrl(Camera camera, StreamProfile profile)
    {
        int channel = profile == StreamProfile.Main ? MainChannel : SubChannel;
        return $"rtsp://{BuildUserInfo(camera)}{camera.IpAddress}:{RtspPort}/Streaming/Channels/{channel}";
    }

    /// <summary>
    /// Kimlik bilgileri percent-encode edilir: parolada <c>#</c>, <c>/</c>, <c>?</c> ya da <c>%</c> olsaydi URL bozulur ve MediaMTX kameraya
    /// baglanamazdi. Kullanici adi yoksa userinfo hic yazilmaz (<c>rtsp://:@ip</c> uretilmesin).
    /// </summary>
    private static string BuildUserInfo(Camera camera)
    {
        if (string.IsNullOrEmpty(camera.Username))
            return "";

        return $"{Uri.EscapeDataString(camera.Username)}:{Uri.EscapeDataString(camera.Password ?? "")}@";
    }
}
