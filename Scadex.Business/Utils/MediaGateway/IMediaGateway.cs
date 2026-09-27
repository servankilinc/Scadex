using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// <b>Medya buradan GECMEZ.</b> Bu gecit yalnizca yapilandirma yapar;
/// Göruntu <c>Kamera -> Media Gateway (MediaMTX) -> Client</c> yolunu izler ve Scadex uzerinden akmaz. <para/>
/// Anlik görüntü ve klip de <c>ICameraCaptureGateway</c> (FFmpeg) ile ayni canli path'den (<c>cam_{id}_main</c>), token ile okunur. Monitoring açıksa kameraya ikinci oturum acilmaz
/// </summary>
public interface IMediaGateway
{
    public const string HttpClientName = "http_client_media_gateway";

    /// <summary> Live stream path'i kurar (path varsa ve ayarlarında değişiklik yoksa atlar değişiklik varsa günceller). </summary>
    Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default);

    /// <summary> Yolu siler. </summary>
    Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary> Path listesini sağlar </summary>
    Task<Result<IReadOnlyList<MediaPathInfo>>> ListPathsAsync(CancellationToken cancellationToken = default);

    #region Static Path Name Generators
    private const string LivePathPrefix = "cam_";

    /// <summary>
    /// Sunucunun MediaMTX'ten okudugu RTSP dinleyicisi. <c>mediamtx.yml => rtspAddress</c> (<c>:8554</c>) ile ESLESMELI; yml'de port degisirse, MediaMTX baska makineye tasinirsa ya da RTSPS acilirsa burasi da degisir. 
    /// MediaMTX ile Proje aynı sunucuda old için localhost sabit yazılır.
    /// </summary>
    private const string InternalRtspBaseUrl = "rtsp://127.0.0.1:8554";

    /// <summary> Canli izleme yolunun adi. <c>Id</c>'den turetilir </summary>
    static string LivePathName(Guid cameraId, StreamProfile profile) => $"{LivePathPrefix}{cameraId:N}_{profile.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Sunucu ici okuyucunun (FFmpeg) canli path'e baglandigi adres. Token PAROLA alanindadir: MediaMTX onu auth hook'a (<c>POST /api/MediaGateway/auth</c>) iletir, tarayicinin token gibi dogrulanir. Kullanici adi onemsizdir sabit girildi.
    /// </summary>
    static string LiveRtspUrl(string pathName, string token) =>
        new UriBuilder(InternalRtspBaseUrl) { UserName = "scadex", Password = token, Path = pathName }.Uri.ToString();

    /// <summary> Yol BIZIM urettigimiz bir path m? Gün sonu temizleyici worker servisi yalnızca bunlara uygulanır. </summary>
    static bool IsManagedPathName(string pathName) => pathName.StartsWith(LivePathPrefix, StringComparison.Ordinal);
    #endregion
}
