using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// <b>Medya buradan GECMEZ.</b> Bu gecit yalnizca MediaMTX'i yapilandirir; goruntu <c>Kaynak -> Media Gateway (MediaMTX) -> Client</c>
/// yolunu izler ve Scadex uzerinden akmaz. <para/>
/// Gecit kaynagi BILMEZ: yol adlari ve kaynak adresleri onu kullanan modullerindir (CameraManagement <c>cam_</c>, RemoteDesk <c>pc_</c>).
/// Yollarin yetkilendirmesi de modullerindir (<see cref="IMediaPathAuthorizer"/>).
/// </summary>
public interface IMediaGateway
{
    public const string HttpClientName = "http_client_media_gateway";

    /// <summary>
    /// Kaynagini MediaMTX'in kendisinin cektigi bir yolu kurar (yol varsa ve yapilandirmasi ayniysa atlar, farkliysa gunceller).
    /// Talep uzerine baglanir: izleyicisi olmayan yolun kaynak oturumu <c>MediaGatewaySettings.SourceOnDemandCloseAfterSec</c> sonra kapanir.
    /// </summary>
    Task<Result> EnsureSourcePathAsync(string pathName, string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yol VARSA ve yapilandirmasi verilen kaynakla uyusmuyorsa siler; yoksa ya da guncelse dokunmaz.
    /// Kaynak bilgisi (IP / marka / kimlik) degistiginde acik izleyicilerin eski oturumu kesilsin diye — yolu yeniden kurmak
    /// bir sonraki izleme isteginin (<see cref="EnsureSourcePathAsync"/>) isidir.
    /// </summary>
    Task<Result> DeleteSourcePathIfStaleAsync(string pathName, string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary> Yolu siler. </summary>
    Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary> Path listesini sağlar </summary>
    Task<Result<IReadOnlyList<MediaPathInfo>>> ListPathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pathin ÇALIŞMA ZAMANI durumu (<c>v3/paths/get</c>); yapılandırmaya bakmaz. Yayıncısı gelmemiş/gitmiş yol için <c>Data = null</c>.
    /// Yapılandırmada olmayan, yayıncıyla yaşayan yollar (RemoteDesk <c>pc_</c>) içindir.
    /// </summary>
    Task<Result<MediaRuntimePath?>> GetRuntimePathAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pathin yayıncısını (RTSP/RTSPS oturumu) zorla kapatır. Yayıncı yoksa başarılıdır. Yeniden yayını engellemek çağıranın işidir
    /// (ör. token önce silmek) — aksi hâlde yayıncı hemen geri bağlanır.
    /// </summary>
    Task<Result> KickPublisherAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sunucu ici okuyucunun (FFmpeg) bir yola baglandigi adres. Token PAROLA alanindadir: MediaMTX onu auth hook'a (<c>POST /api/MediaGateway/auth</c>) iletir,
    /// yolu ustlenen <see cref="IMediaPathAuthorizer"/> tarayicinin token'i gibi dogrular. Kullanici adi onemsizdir sabit girildi.
    /// Host <c>127.0.0.1</c>: MediaMTX ile API ayni sunucuda. Port <c>MediaGatewaySettings.RtspPort</c> (<c>mediamtx.yml &gt; rtspAddress</c> ile eslesmeli).
    /// </summary>
    // exp => "rtsp://127.0.0.1:8554/path"
    static string LiveRtspUrl(int rtspPort, string pathName, string token) =>
        new UriBuilder("rtsp", "127.0.0.1", rtspPort, pathName) { UserName = "scadex", Password = token }.Uri.ToString();
}
