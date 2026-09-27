using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;

namespace Scadex.Business.Utils.CameraCaptureGateway;

/// <summary>
/// Bir RTSP kaynagindan anlik goruntu (JPEG) ve klip (MP4) alir (FFmpeg). Markadan bagimsizdir, kameraya DOGRUDAN baglanmaz:
/// kaynak MediaMTX'in canli yoludur (<c>CameraService.PrepareCaptureSourceAsync</c>, biletli). Marka API'si (ISAPI vb.) kullanilmaz.
/// </summary>
public interface ICameraCaptureGateway
{
    /// <summary> Kaynaktan tek kare alir (JPEG). Ilk anahtar kare beklenir. </summary>
    Task<Result<SnapshotPayload>> GetSnapshotAsync(Camera camera, string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary> Sessiz olarak <paramref name="outputFullPath"/>'e MP4 olarak yazar; sure dolunca doner. </summary>
    Task<Result> RecordClipAsync(Camera camera, string sourceUrl, int durationSec, string outputFullPath, CancellationToken cancellationToken = default);
}


/// <summary>Kameradan alinan tek kare.</summary>
/// <param name="Content">Ham baytlar — <b>veritabanina YAZILMAZ</b>.</param>
/// <param name="ContentType">Her zaman <c>image/jpeg</c>.</param>
public sealed record SnapshotPayload(byte[] Content, string ContentType);