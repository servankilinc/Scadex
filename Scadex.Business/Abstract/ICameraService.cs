using Scadex.Business.Utils.SnapshotGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Dtos.Common;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Abstract;

public interface ICameraService
{
    Task<Result<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CameraDto>>> GetListAsync(Guid cabinetId, bool includePassive = false, CancellationToken cancellationToken = default);
    Task<Result<CreatedDto>> CreateAsync(CameraCreateDto request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(CameraUpdateDto request, CancellationToken cancellationToken = default);

    #region Monitoring
    /// <summary> Bir yoklama (ping / TCP connect) isteği atar ve sonucunu yazar. </summary>
    Task<Result> RecordProbeResultAsync(Guid cameraId, CameraProbeResultDto result, CancellationToken cancellationToken = default);
    #endregion

    #region Streaming (MediaMTX)
    /// <summary> Canli izleme token'ı uretir ve MediaMTX ilgili path'i kurar. NOT: Token path'e baglidir ve kisa omurludur. </summary>
    Task<Result<StreamTokenDto>> CreateStreamTokenAsync(Guid cameraId, StreamProfile profile, CancellationToken cancellationToken = default);

    /// <summary> MediaMTX token'ı dogrular. </summary>
    Task<bool> ValidateStreamTokenAsync(string? path, string? ticket, CancellationToken cancellationToken = default);
    #endregion

    #region Snapshot
    /// <summary> Hiçbir yere kaydedilmeden anlik goruntu(test ve light önzileme için), kisa omurlu onbellek'de tutulur. </summary>
    /// <param name="fresh"> <c>true</c> ise onbellek atlanir. Tekrar görüntü alınır.
    /// </param>
    Task<Result<SnapshotPayload>> GetSnapshotAsync(Guid cameraId, bool fresh = false, CancellationToken cancellationToken = default);
    #endregion

    #region Capture
    /// <summary> Kameranin son cekimleri, yeniden eskiye. </summary>
    Task<Result<ICollection<CameraCaptureDto>>> GetCapturesAsync(Guid cameraId, int take = 20, CancellationToken cancellationToken = default);

    /// <summary> Anlik görüntü veya video kaydı, senkron tamamlanir; <c>Pending</c> doner ve arka planda surer. </summary>
    Task<Result<CameraCaptureDto>> CreateCaptureAsync(Guid cameraId, CameraCaptureCreateDto request, CancellationToken cancellationToken = default);

    /// <summary> Kuyruga alinmis bir klip cekimini yurutur. Yalnizca <c>ClipCaptureWorker</c> cagirir; HTTP yolundan erisilmez. </summary>
    Task RunClipCaptureAsync(long captureId, CancellationToken cancellationToken = default); 
    #endregion
}
