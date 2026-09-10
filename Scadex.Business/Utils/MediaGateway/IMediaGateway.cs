using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// <b>Medya buradan GECMEZ.</b> Bu gecit yalnizca yapilandirma yapar; 
/// Göruntu <c>Kamera -> Media Gateway (MediaMTX) -> Client</c> yolunu izler ve Scadex uzerinden akmaz (sanpshot hariç).
/// </summary>
public interface IMediaGateway
{
    public const string HttpClientName = "http_client_media_gateway";

    /// <summary> Live stream path'i kurar (path varsa ve ayarlarında değişiklik yoksa atlar değişiklik varsa günceller). </summary>
    Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default);

    /// <summary> Klip cekimi icin, gecici bir path kurar. </summary>
    /// <param name="recordPath"> MediaMTX'in segmentleri yazacagi yol. Zaman yer tutucusu (<c>%Y</c>, <c>%M</c> ...) icermek ZORUNDA. </param>
    /// <param name="segmentDuration"> Segment suresi (<c>"13s"</c> gibi). Klip suresinden uzun tutulur ki istenen sure tek bir dosyaya düşsün. </param>
    Task<Result> EnsureClipPathAsync(Camera camera, long captureId, string recordPath, string segmentDuration, CancellationToken cancellationToken = default);

    /// <summary> Yolu siler. </summary>
    Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary> Path listesini sağlar </summary>
    Task<Result<IReadOnlyList<MediaPathInfo>>> ListPathsAsync(CancellationToken cancellationToken = default);

    #region Static Path Name Generators
    private const string LivePathPrefix = "cam_";
    private const string ClipPathPrefix = "clip_";

    /// <summary> Canli izleme yolunun adi. <c>Id</c>'den turetilir </summary>
    static string LivePathName(Guid cameraId, StreamProfile profile) => $"{LivePathPrefix}{cameraId:N}_{profile.ToString().ToLowerInvariant()}";

    /// <summary> Klip cekiminin gecici path adı. </summary>
    static string ClipPathName(long captureId) => $"{ClipPathPrefix}{captureId}";

    /// <summary> Yol BIZIM urettigimiz bir yol mu? Temizlik yalnizca bunlara uygulanır. </summary>
    static bool IsManagedPathName(string pathName) =>
        pathName.StartsWith(LivePathPrefix, StringComparison.Ordinal) ||
        pathName.StartsWith(ClipPathPrefix, StringComparison.Ordinal);

    /// <summary> Yol bir klip cekimine mi ait? </summary>
    static bool IsClipPathName(string pathName) => pathName.StartsWith(ClipPathPrefix, StringComparison.Ordinal);
    #endregion
}
