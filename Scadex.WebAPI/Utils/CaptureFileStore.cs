using Scadex.Business.Abstract;
using Scadex.Business.Utils.CaptureFileStore;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.WebAPI.Utils;

public sealed class CaptureFileStore : ICaptureFileStore
{
    private readonly IWebHostEnvironment _environment;
    private readonly ICameraCaptureSettingService _cameraCaptureSettingService;
    private readonly ILogger<CaptureFileStore> _logger;

    public CaptureFileStore(IWebHostEnvironment environment, ICameraCaptureSettingService cameraCaptureSettingService, ILogger<CaptureFileStore> logger)
    {
        _environment = environment;
        _cameraCaptureSettingService = cameraCaptureSettingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<StoredCapture>> SaveSnapshotAsync(byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        string extension = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

        try
        {
            var (fullPath, relativePath) = BuildTargetPath(await CaptureRootAsync(cancellationToken), extension);
            await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
            return Result<StoredCapture>.Success(new StoredCapture(relativePath, content.LongLength));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Snapshot görüntü diske yazılamadi");
            return Result<StoredCapture>.Failure(description: "Görüntü diske yazılamadı.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoredCapture>> MoveClipAsync(string sourceFullPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourceFullPath))
                return Result<StoredCapture>.Failure(description: "Klip dosyası bulunamadı.");

            long size = new FileInfo(sourceFullPath).Length;
            var (fullPath, relativePath) = BuildTargetPath(await CaptureRootAsync(cancellationToken), ".mp4");

            File.Move(sourceFullPath, fullPath, overwrite: false);

            return Result<StoredCapture>.Success(new StoredCapture(relativePath, size));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Klip dosyasi tasinamadi: {Source}", sourceFullPath);
            return Result<StoredCapture>.Failure(description: "Klip dosyası kaydedilemedi.");
        }
    }

    /// <inheritdoc />
    public void TryDeleteTempDirectory(string fullPath)
    {
        try
        {
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Gecici klip klasoru silinemedi: {Path}", fullPath);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryDeleteCaptureAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        try
        {
            string webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            string captureRoot = Path.GetFullPath(Path.Combine(webRoot, (await CaptureRootAsync(cancellationToken)).Replace('/', Path.DirectorySeparatorChar)));
            string fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            // Yol cekim kokunun DISINA cikiyorsa dokunulmaz. Deger bizim yazdigimiz
            // bir kolondan geliyor, ama silme geri alinamaz: bozuk/elle degistirilmis
            // tek bir satirin wwwroot disinda bir dosyayi silmesine izin verilemez.
            if (!fullPath.StartsWith(captureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Cekim dosyasi cekim kokunun disinda, silinmedi: {Path}", relativePath);
                return false;
            }

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cekim dosyasi silinemedi: {Path}", relativePath);
            return false;
        }
    }

    #region Helpers
    /// <summary>Çekim kökü artık veritabanından gelir; her kullanımda tazelenir.</summary>
    private async Task<string> CaptureRootAsync(CancellationToken cancellationToken) =>
        (await _cameraCaptureSettingService.GetSettingsAsync(cancellationToken)).CaptureRoot.Trim('/');

    private (string FullPath, string RelativePath) BuildTargetPath(string captureRoot, string extension)
    {
        string relativeFolder = $"{captureRoot}/{DateTime.UtcNow:yyyy/MM/dd}";
        string fileName = $"{Guid.NewGuid():N}{extension}";

        string webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        string folder = Path.Combine(webRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folder);

        return (Path.Combine(folder, fileName), $"{relativeFolder}/{fileName}");
    } 
    #endregion
}
