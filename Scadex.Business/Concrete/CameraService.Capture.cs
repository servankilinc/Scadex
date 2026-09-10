using Scadex.Business.Utils.MediaGateway;
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
        string pathName = IMediaGateway.ClipPathName(captureId);

        // Klasor adi Path adından belirlenir, MediaMTX recordPath'teki %path yer tutucusunu yol adiyla doldurur,
        // MediaMTX'in kayit ettigi dosya oraya gelir. Sonra biz onu bulup wwwroot altında kalıcı saklarız.
        string tempFolder = Path.Combine(_mediaGatewaySettings.RecordRoot, pathName);

        // Yol GERCEKTEN kuruldu mu? sonucuna göre temp klasörü silinir
        bool pathCreated = false;

        try
        {
            // Segment suresi klip suresinden UZUN: boylece istenen sure tek bir dosyaya duser. Esit olsaydi rotasyon tam sinirda gerceklesip goruntuyu iki dosyaya bolebilirdi.
            string segmentDuration = $"{duration + (_captureSettings.ClipFinalizeGraceMs / 1000) + 5}s";

            // recordPath %path ICERMEK ZORUNDA
            string recordPath = Path.Combine(_mediaGatewaySettings.RecordRoot, "%path", "%Y-%m-%d_%H-%M-%S-%f").Replace('\\', '/');

            var ensureResult = await _mediaGateway.EnsureClipPathAsync(camera, captureId, recordPath, segmentDuration, cancellationToken);

            if (!ensureResult.IsSuccess)
            {
                await FailCaptureAsync(capture, ensureResult.Error.Description, cancellationToken);
                return;
            }

            pathCreated = true;

            // Kaydin FIILEN basladigi an
            capture.CapturedAtUtc = DateTime.UtcNow;

            await Task.Delay(TimeSpan.FromMilliseconds(duration * 1000 + _captureSettings.ClipFinalizeGraceMs), cancellationToken);

            // Path silmek MediaMTX'in kaydi sonlandirmasini saglar: fmp4 segmenti ancak kapandiginda oynatilabilir hale gelir.
            await _mediaGateway.DeletePathAsync(pathName, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(_captureSettings.ClipFinalizeGraceMs), cancellationToken);

            string? clipFile = FindNewestClip(tempFolder);
            if (clipFile == null)
            {
                await FailCaptureAsync(capture, "Medya geçidi klip dosyası üretmedi. Kameraya bağlanılamamış olabilir.", cancellationToken);
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

            _captureFileStore.TryDeleteTempDirectory(tempFolder);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError($"Klip cekimi {captureId} basarisiz", exception);
            await FailCaptureAsync(capture, "Klip çekimi sırasında beklenmeyen bir hata oluştu.", cancellationToken);
        }
        finally
        {
            // Kurulmus bir yol HER KOSULDA dusurulmeli
            if (pathCreated)
                await _mediaGateway.DeletePathAsync(pathName, CancellationToken.None);
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
            ExpiresAt = _captureSettings.CaptureRetentionDays > 0 ? DateTime.UtcNow.AddDays(_captureSettings.CaptureRetentionDays) : null,
            RequestedByUserId = currentUserId
        };

        // 3) Çekim türüne göre işlem yapılır
        if (request.Type == CaptureType.Snapshot)
        {
            var snapshotResult = await _snapshotGateway.GetSnapshotAsync(camera, cancellationToken);

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
            if (duration > _captureSettings.MaxClipDurationSec)
                return Result<CameraCaptureDto>.Validation(new Dictionary<string, string[]> { ["DurationSec"] = [$"Klip süresi en fazla {_captureSettings.MaxClipDurationSec} saniye olabilir."] });

            if (string.IsNullOrWhiteSpace(_mediaGatewaySettings.RecordRoot))
                return Result<CameraCaptureDto>.Failure(description: "Klip çekimi yapılandırılmamış: Media Gateway:RecordRoot tanımlı değil.");

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
            if (!_captureFileStore.TryDeleteCapture(capture.RelativePath!)) continue;

            capture.RelativePath = null;
            purged++;
        }

        if (purged > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return purged;
    }

    #region Helpers
    /// <summary>
    /// Gecici klasordeki en yeni klip dosyasi.
    ///
    /// MediaMTX dosya adini zaman sablonundan uretir, dolayisiyla adi onceden
    /// bilemiyoruz — klasore bakmak tek yol.
    /// </summary>
    private static string? FindNewestClip(string folder)
    {
        if (!Directory.Exists(folder)) return null;

        return new DirectoryInfo(folder)
            .GetFiles("*.mp4", SearchOption.AllDirectories)
            .Where(f => f.Length > 0)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private async Task FailCaptureAsync(CameraCapture capture, string? reason, CancellationToken cancellationToken)
    {
        capture.Status = CaptureStatus.Failed;
        capture.FailureReason = reason?.Truncate(512);
        capture.RelativePath = null;
        await _unitOfWork.CameraCaptures.UpdateAndSaveAsync(capture, cancellationToken);
    }
    #endregion
}
