using Scadex.Business.Settings;
using Scadex.Business.Utils.CaptureFileStore;
using Scadex.Core.Utils.ResultPattern;

namespace Scadex.WebAPI.Utils;

public sealed class CaptureFileStore : ICaptureFileStore
{
    private readonly IWebHostEnvironment _environment;
    private readonly CameraCaptureSettings _settings;
    private readonly ILogger<CaptureFileStore> _logger;

    public CaptureFileStore(IWebHostEnvironment environment, CameraCaptureSettings settings, ILogger<CaptureFileStore> logger)
    {
        _environment = environment;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<StoredCapture>> SaveSnapshotAsync(byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        string extension = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

        try
        {
            var (fullPath, relativePath) = BuildTargetPath(extension);
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
            var (fullPath, relativePath) = BuildTargetPath(".mp4");

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

    #region Helpers
    private (string FullPath, string RelativePath) BuildTargetPath(string extension)
    {
        string relativeFolder = $"{_settings.CaptureRoot.Trim('/')}/{DateTime.UtcNow:yyyy/MM/dd}";
        string fileName = $"{Guid.NewGuid():N}{extension}";

        string webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        string folder = Path.Combine(webRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folder);

        return (Path.Combine(folder, fileName), $"{relativeFolder}/{fileName}");
    } 
    #endregion
}
