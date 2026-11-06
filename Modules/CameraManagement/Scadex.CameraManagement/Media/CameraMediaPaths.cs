using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.CameraManagement.Media;

/// <summary> Kameranın MediaMTX yol adları. <c>cam_</c> öneki bu modülündür; başka bir modül bu öneki üstlenmemeli (<c>IMediaPathAuthorizer</c>). </summary>
public static class CameraMediaPaths
{
    public const string LivePathPrefix = "cam_";

    /// <summary> Canli izleme yolunun adi. <c>Id</c>'den turetilir </summary>
    public static string LivePathName(Guid cameraId, StreamProfile profile) => $"{LivePathPrefix}{cameraId:N}_{profile.ToString().ToLowerInvariant()}";

    /// <summary> Yol BIZIM urettigimiz bir path mi? Gün sonu temizleyici worker servisi ve auth kancası yalnızca bunlara uygulanır. </summary>
    public static bool IsManagedPathName(string pathName) => pathName.StartsWith(LivePathPrefix, StringComparison.Ordinal);
}
