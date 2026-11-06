using Scadex.CameraManagement.Capture;
using Scadex.CameraManagement.Model.Dtos.Capture.Commands;
using Scadex.CameraManagement.Model.Dtos.Capture.Queries;
using Scadex.CameraManagement.Model.Dtos.Streaming.Queries;
using Scadex.Core.Utils.ResultPattern;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.CameraManagement.Services.Abstract;

/// <summary>
/// Kameranın MEDYA tarafı: canlı izleme bileti, anlık görüntü, kare/klip çekimi ve MediaMTX <c>cam_</c> yolları. Kameranın cihaz bilgisi
/// (ad, IP, marka, kimlik) çekirdektedir (<c>ICameraService</c>, <c>Camera</c> tablosu) ve buradan yalnızca projeksiyonla okunur.
/// Çözümler (Signalization) kare çekmek ve kare okumak için bu servisi kullanır.
/// </summary>
public interface ICameraMediaService
{
    #region Streaming (MediaGateway)
    /// <summary>
    /// Kamera bilgisi değişen / pasife alınan cihazların canlı yollarını medya geçidinde yeniler — pasif kameranın yolunu siler, aktif
    /// kamerada yapılandırması güncel RTSP kaynağıyla uyuşmayan yolu siler (açık izleyicilerin eski bilgilerle kurulan oturumu kesilsin diye).
    /// Kamera olmayan Id'ler yok sayılır. Başarısızlık yalnızca loglanır. Çağıran: <c>CameraDeviceChangeObserver</c> (commit sonrası).
    /// </summary>
    Task RefreshStreamPathsAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken = default);

    /// <summary> Canli izleme token'ı uretir ve MediaMTX ilgili path'i kurar. NOT: Token path'e baglidir ve kisa omurludur. </summary>
    Task<Result<StreamTokenDto>> CreateStreamTokenAsync(Guid cameraId, StreamProfile profile, CancellationToken cancellationToken = default);

    /// <summary> MediaMTX token'ı dogrular. </summary>
    Task<bool> ValidateStreamTokenAsync(string? path, string? ticket, CancellationToken cancellationToken = default);
    #endregion

    #region Snapshot
    /// <summary> Hiçbir yere kaydedilmeden anlik goruntu(test ve light önzileme için), kisa omurlu onbellek'de tutulur. </summary>
    /// <param name="fresh"> <c>true</c> ise onbellek atlanir. Tekrar görüntü alınır. NOT: eğer capture alınacaksa güncel görüntü alınmalıdır. </param>
    Task<Result<SnapshotPayload>> GetSnapshotAsync(Guid cameraId, bool fresh = false, CancellationToken cancellationToken = default);
    #endregion

    #region Capture
    /// <summary> Kameranin son cekimleri, yeniden eskiye. </summary>
    Task<Result<ICollection<CameraCaptureDto>>> GetCapturesAsync(Guid cameraId, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen Id'lerin çekimleri (bulunamayan Id atlanır). Çözümlerin (Signalization oturum raporu) kendi tablolarında tuttuğu
    /// <c>CameraCaptureId</c>'leri çözmesi içindir — modülün tablosunu başka bir context'e eşlemek yerine.
    /// </summary>
    Task<ICollection<CameraCaptureDto>> GetCapturesByIdsAsync(IReadOnlyCollection<long> captureIds, CancellationToken cancellationToken = default);

    /// <summary> Anlık görüntü veya video kaydı, senkron tamamlanir; <c>Pending</c> döner ve arka planda devam eder diske yazar. </summary>
    Task<Result<CameraCaptureDto>> CreateCaptureAsync(Guid cameraId, CameraCaptureCreateDto request, CancellationToken cancellationToken = default);

    /// <summary> 
    /// Kuyruga alinmis bir klip cekimini yurutur. Yalnizca <c>ClipCaptureWorker</c> cagirir; HTTP yolundan erisilmez. 
    /// Akis: MediaMTX canli yolundan (token) geciçi bir MP4'e sure kadar kayit (FFmpeg) → dosyayi kalici konuma tasi → gecici klasoru sil.
    /// </summary>
    Task RunClipCaptureAsync(long captureId, CancellationToken cancellationToken = default);

    /// <summary> Saklama suresi dolmus cekimlerin DOSYASINI siler ve </summary>
    Task<int> PurgeExpiredCaptureFilesAsync(CancellationToken cancellationToken = default);
    #endregion
}
