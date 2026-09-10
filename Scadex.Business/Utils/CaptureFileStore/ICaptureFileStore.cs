using Scadex.Core.Utils.ResultPattern;

namespace Scadex.Business.Utils.CaptureFileStore;

public sealed record StoredCapture(string RelativePath, long SizeBytes);

public interface ICaptureFileStore
{
    /// <summary>Anlik goruntuyu tarihli klasore yazar.</summary>
    Task<Result<StoredCapture>> SaveSnapshotAsync(byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary> Media Gateway urettigi klip dosyasini kalici konuma tasir. Kaynak zaten gecici bir dizindedir. </summary>
    Task<Result<StoredCapture>> MoveClipAsync(string sourceFullPath, CancellationToken cancellationToken = default);

    /// <summary> Media Gateway'in klip icin kullandigi gecici klasoru siler. </summary>
    void TryDeleteTempDirectory(string fullPath);

    /// <summary> Saklama suresi dolmus bir cekim dosyasini siler. </summary>
    bool TryDeleteCapture(string relativePath);
}
