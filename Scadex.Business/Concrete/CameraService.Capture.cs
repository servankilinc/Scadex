using Scadex.Business.Utils.CameraCaptureGateway;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;


namespace Scadex.Business.Concrete;

public partial class CameraService
{
    /// <inheritdoc/>
    public async Task<Result<ICollection<CameraCaptureDto>>> GetCapturesAsync(Guid cameraId, int take = 20, CancellationToken cancellationToken = default)
    {
        bool cameraExists = await _unitOfWork.Cameras.IsExistAsync(where: c => c.Id == cameraId, cancellationToken: cancellationToken);

        if (!cameraExists)
            return Result<ICollection<CameraCaptureDto>>.NotFound(description: "Kamera bulunamadi");

        // Sinir hem alttan hem ustten: 0 ve negatif anlamsiz, sinirsiz ise cekim gecmisi buyudukce tum tabloyu okumak demek.
        int safeTake = Math.Clamp(take, 1, 200);

        var captures = await _unitOfWork.CameraCaptures.GetRecentForCameraAsync(cameraId, safeTake, cancellationToken);

        return Result<ICollection<CameraCaptureDto>>.Success(_mapper.Map<List<CameraCaptureDto>>(captures));
    }

    /// <inheritdoc/>
    public async Task RunClipCaptureAsync(long captureId, CancellationToken cancellationToken = default)
    {
        var capture = await _unitOfWork.CameraCaptures.GetAsync(where: c => c.Id == captureId, tracking: true, cancellationToken: cancellationToken);
        if (capture == null)
        {
            _logger.LogWarning($"Klip cekimi {captureId} bulunamadi; atlaniyor.");
            return;
        }

        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == capture.CameraId, cancellationToken: cancellationToken);
        if (camera == null)
        {
            await FailCaptureAsync(capture, "Kamera bulunamadı.", cancellationToken);
            return;
        }

        int duration = capture.DurationSec ?? 0;

        // Çekim başına ayrı geçici klasör: paralel cekimler birbirinin dosyasina dokunmaz. Dosya wwwroot'a TAMAMLANINCA taşınır
        string tempFolder = Path.Combine(Path.GetTempPath(), "scadex-clips", captureId.ToString());
        string clipFile = Path.Combine(tempFolder, "clip.mp4");

        try
        {
            Directory.CreateDirectory(tempFolder);

            // Kaydin basladigi an (ilk anahtar kare beklemesi — kimse izlemiyorsa MediaMTX'in kameraya baglanmasi da — ~1-3 sn bunun uzerine eklenir).
            capture.CapturedAtUtc = DateTime.UtcNow;

            // Kaynak MediaMTX'in canli path'i; token hemen kullanilacagi icin kayittan HEMEN ONCE alinir (kuyrukta beklerken degil).
            var source = await PrepareCaptureSourceAsync(camera, cancellationToken);
            if (!source.IsSuccess)
            {
                await FailCaptureAsync(capture, source.Error.Description, cancellationToken);
                return;
            }

            var recordResult = await _captureGateway.RecordClipAsync(camera, source.Data, duration, clipFile, cancellationToken);
            if (!recordResult.IsSuccess)
            {
                await FailCaptureAsync(capture, recordResult.Error.Description, cancellationToken);
                return;
            }

            var storeResult = await _captureFileStore.MoveClipAsync(clipFile, cancellationToken);
            if (!storeResult.IsSuccess)
            {
                await FailCaptureAsync(capture, storeResult.Error.Description, cancellationToken);
                return;
            }

            capture.Status = CaptureStatus.Available;
            capture.RelativePath = storeResult.Data.RelativePath;
            capture.SizeBytes = storeResult.Data.SizeBytes;

            await _unitOfWork.CameraCaptures.UpdateAndSaveAsync(capture, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError($"Klip cekimi {captureId} basarisiz", exception);
            await FailCaptureAsync(capture, "Klip çekimi sırasında beklenmeyen bir hata oluştu.", cancellationToken);
        }
        finally
        {
            // Gecici klasor HER KOSULDA silinir: basarida dosya zaten tasinmistir, hatada yarim MP4 birakilmaz.
            _captureFileStore.TryDeleteTempDirectory(tempFolder);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<CameraCaptureDto>> CreateCaptureAsync(Guid cameraId, CameraCaptureCreateDto request, CancellationToken cancellationToken = default)
    {
        // 1) Valisayon ve kamera kontrolu
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CameraCaptureDto>.Validation(validationResult.Failures, description: "Validation failed for CameraCaptureCreateDto");

        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == cameraId && c.IsActive, cancellationToken: cancellationToken);
        if (camera == null)
            return Result<CameraCaptureDto>.NotFound(description: "Kamera bulunamadi veya pasif durumda.");

        var captureSettings = await _captureSettingService.GetSettingsAsync(cancellationToken);

        // 2) Capture oluşturulur
        var identifier = _httpContextManager.GetNameIdentifier();
        Guid? currentUserId = identifier.IsSuccess && Guid.TryParse(identifier.Data, out var userId) ? userId : null;

        var capture = new CameraCapture()
        {
            CameraId = cameraId,
            Type = request.Type,
            Status = CaptureStatus.Pending,
            CapturedAtUtc = DateTime.UtcNow,
            DurationSec = null,
            // saklanma süresi
            ExpiresAt = captureSettings.CaptureRetentionDays > 0 ? DateTime.UtcNow.AddDays(captureSettings.CaptureRetentionDays) : null,
            RequestedByUserId = currentUserId
        };

        // 3) Çekim türüne göre işlem yapılır
        if (request.Type == CaptureType.Snapshot)
        {
            var source = await PrepareCaptureSourceAsync(camera, cancellationToken);
            var snapshotResult = source.IsSuccess
                ? await _captureGateway.GetSnapshotAsync(camera, source.Data, cancellationToken)
                : Result<SnapshotPayload>.Failure(description: source.Error.Description);

            // Delil zamani = karenin geldigi an. FFmpeg baglanip anahtar kareyi beklerken 1-3 sn gecer; istek anini yazmak zamani kaydirirdi.
            capture.CapturedAtUtc = DateTime.UtcNow;

            if (!snapshotResult.IsSuccess)
            {
                capture.Status = CaptureStatus.Failed;
                capture.FailureReason = snapshotResult.Error.Description.Truncate(512);
            }
            else
            {
                var storeResult = await _captureFileStore.SaveSnapshotAsync(snapshotResult.Data.Content, snapshotResult.Data.ContentType, cancellationToken);

                if (!storeResult.IsSuccess)
                {
                    capture.Status = CaptureStatus.Failed;
                    capture.FailureReason = storeResult.Error.Description.Truncate(512);
                }
                else
                {
                    capture.Status = CaptureStatus.Available;
                    capture.RelativePath = storeResult.Data.RelativePath;
                    capture.SizeBytes = storeResult.Data.SizeBytes;
                }
            }

            await _unitOfWork.CameraCaptures.AddAndSaveAsync(capture, cancellationToken);
            return Result<CameraCaptureDto>.Success(_mapper.Map<CameraCaptureDto>(capture));
        }
        else if (request.Type == CaptureType.Clip)
        {
            int duration = request.DurationSec.HasValue ? request.DurationSec.Value : 5; // varsayılan klip süresi 5 saniye
            if (duration > captureSettings.MaxClipDurationSec)
                return Result<CameraCaptureDto>.Validation(new Dictionary<string, string[]> { ["DurationSec"] = [$"Klip süresi en fazla {captureSettings.MaxClipDurationSec} saniye olabilir."] });

            capture.DurationSec = duration;
            capture.Status = CaptureStatus.Pending;

            await _unitOfWork.CameraCaptures.AddAndSaveAsync(capture, cancellationToken);

            // TODO: Kuyruk BELLEK ICI: uygulama bu noktadan sonra yeniden baslarsa satir Pending olarak asili kalir. Kalici bir kuyruk bu turda yazılmalı
            _clipCaptureQueue.Enqueue(capture.Id);

            return Result<CameraCaptureDto>.Success(_mapper.Map<CameraCaptureDto>(capture));
        }
        else
        {
            return Result<CameraCaptureDto>.Validation(new Dictionary<string, string[]> { ["Type"] = ["Desteklenmeyen çekim türü."] });
        }
    }


    /// <inheritdoc/>
    public async Task<int> PurgeExpiredCaptureFilesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // SATIR SILINMEZ, yalnizca dosya. Cekimin yapildigi bilgisi gecmiste kalir;
        // dosyanin gittigi RelativePath'in null olmasindan anlasilir.
        // ExpiresAt null olan cekim suresizdir (CaptureRetentionDays = 0).
        var expired = await _unitOfWork.CameraCaptures.GetAllAsync(
            where: c => c.ExpiresAt != null && c.ExpiresAt <= now && c.RelativePath != null,
            tracking: true,
            cancellationToken: cancellationToken) ?? [];

        if (expired.Count == 0) return 0;

        int purged = 0;

        foreach (var capture in expired)
        {
            // Dosya silinemediyse RelativePath KORUNUR: kolonu null'lamak, diskte duran
            // dosyayi bir daha bulunamaz hale getirir ve kalici cop birakirdi.
            if (!await _captureFileStore.TryDeleteCaptureAsync(capture.RelativePath!, cancellationToken)) continue;

            capture.RelativePath = null;
            purged++;
        }

        if (purged > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return purged;
    }

    #region Helpers
    private async Task FailCaptureAsync(CameraCapture capture, string? reason, CancellationToken cancellationToken)
    {
        capture.Status = CaptureStatus.Failed;
        capture.FailureReason = reason?.Truncate(512);
        capture.RelativePath = null;
        await _unitOfWork.CameraCaptures.UpdateAndSaveAsync(capture, cancellationToken);
    }
    #endregion
}
