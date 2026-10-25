using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.RemoteDesk.Contracts.Hub;

namespace Scadex.RemoteDesk.Windows.Service.Services.FileTransfer;

public interface IFileTransferService
{
    /// <summary> PC'de bir klasörü (ya da sürücü köklerini) listeler. </summary>
    DirectoryListing ListDirectory(ListDirectoryCommand command);

    /// <summary> PC'den merkeze dosya yükler (indirme yönü): <c>Path</c>'teki dosyayı okur, merkeze akıtır. </summary>
    Task<FileTransferResult> UploadFileAsync(FileUploadCommand command, CancellationToken cancellationToken);

    /// <summary> Merkezden PC'ye dosya indirir (yükleme yönü): hedef yolu korumalı köklere karşı denetler, sonra yazar. </summary>
    Task<FileTransferResult> DownloadFileAsync(FileDownloadCommand command, CancellationToken cancellationToken);

    /// <summary> Bir transfer başladı (true) / bitti (false) — gösterge için. UI iş parçacığına taşınmalıdır. </summary>
    event Action<bool>? ActiveChanged;
}

/// <summary>
/// Uzak PC tarafında dosya transferi. Dizin listesi hub'ın client-result'ı ile döner; dosya baytları merkezle
/// biletli HTTP üzerinden aktarılır (<c>{CentralApiUrl}/api/RemoteDesk/filetransfer/{id}/push|pull?ticket=</c>). Agent SYSTEM çalışır:
/// her yol <see cref="FilePathPolicy"/>'den geçer. PC'ye yazma ATOMİKTİR — önce <c>&lt;ad&gt;.scadex-part</c>, bayt sayısı tutarsa yerine taşınır;
/// yarıda kalan aktarım mevcut dosyayı bozmaz.
/// </summary>
public sealed class FileTransferService : IFileTransferService
{
    private const int MaxEntries = 5000;
    private const string PartSuffix = ".scadex-part";

    private readonly RemoteDeskClientOptions _options;
    private readonly FilePathPolicy _paths;
    private readonly ILogger<FileTransferService> _logger;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private int _active;

    public FileTransferService(IOptions<RemoteDeskClientOptions> options, FilePathPolicy paths, ILogger<FileTransferService> logger)
    {
        _options = options.Value;
        _paths = paths;
        _logger = logger;
        // push/pull uçları biletle birlikte PC'nin Basic Auth kimliğini de ister (bilet cihaza bağlı, tek kullanımlık — merkez P0.5).
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _options.BasicAuthorization());
    }

    public event Action<bool>? ActiveChanged;

    public DirectoryListing ListDirectory(ListDirectoryCommand command)
    {
        string? path = command.Path;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // Sürücü kökleri.
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new DirEntry(d.Name, true, 0, DateTime.MinValue))
                    .ToArray();
                return new DirectoryListing("", true, null, drives, null);
            }

            if (FilePathPolicy.RejectRead(path) is { } rejected)
                return new DirectoryListing(path, false, "", [], rejected);

            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
                return new DirectoryListing(path, false, ParentOf(dir), [], "Klasör bulunamadı.");

            var entries = new List<DirEntry>();
            foreach (var sub in SafeEnumerate(() => dir.EnumerateDirectories()))
            {
                entries.Add(new DirEntry(sub.Name, true, 0, Modified(sub)));
                if (entries.Count >= MaxEntries) break;
            }
            foreach (var file in SafeEnumerate(() => dir.EnumerateFiles()))
            {
                entries.Add(new DirEntry(file.Name, false, SafeLength(file), Modified(file)));
                if (entries.Count >= MaxEntries) break;
            }
            string? truncated = entries.Count >= MaxEntries ? $"Çok fazla öğe; ilk {MaxEntries} gösteriliyor." : null;
            return new DirectoryListing(dir.FullName, false, ParentOf(dir), [.. entries], truncated);
        }
        catch (UnauthorizedAccessException)
        {
            return new DirectoryListing(path ?? "", false, null, [], "Bu klasöre erişim yok.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dosya transferi: klasör listelenemedi {Path}", path);
            return new DirectoryListing(path ?? "", false, null, [], "Klasör okunamadı.");
        }
    }

    public async Task<FileTransferResult> UploadFileAsync(FileUploadCommand command, CancellationToken cancellationToken)
    {
        BeginActive();
        try
        {
            if (FilePathPolicy.RejectRead(command.Path) is { } rejected)
                return new FileTransferResult(command.TransferId, false, 0, rejected);
            if (!File.Exists(command.Path))
                return new FileTransferResult(command.TransferId, false, 0, "Dosya bulunamadı.");

            long length = new FileInfo(command.Path).Length;
            await using var source = new FileStream(command.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            using var content = new StreamContent(source);
            using var response = await _http.PutAsync(PushUrl(command.TransferId, command.Ticket), content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new FileTransferResult(command.TransferId, false, 0, $"Merkez {(int)response.StatusCode} döndü.");

            _logger.LogInformation("Dosya transferi: {Path} merkeze yüklendi ({Bytes} bayt)", command.Path, length);
            return new FileTransferResult(command.TransferId, true, length, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new FileTransferResult(command.TransferId, false, 0, "Dosyaya erişim yok.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Dosya transferi: yükleme başarısız {Path}", command.Path);
            return new FileTransferResult(command.TransferId, false, 0, "Dosya okunamadı.");
        }
        finally { EndActive(); }
    }

    public async Task<FileTransferResult> DownloadFileAsync(FileDownloadCommand command, CancellationToken cancellationToken)
    {
        BeginActive();
        string? partPath = null;
        try
        {
            if (_paths.RejectWrite(command.DestinationPath, command.FileName, out string fullPath) is { } rejected)
            {
                _logger.LogWarning("Dosya transferi: yazma reddedildi {Dest} / {FileName} — {Reason}", command.DestinationPath, command.FileName, rejected);
                return new FileTransferResult(command.TransferId, false, 0, rejected);
            }

            using var response = await _http.GetAsync(PullUrl(command.TransferId, command.Ticket), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new FileTransferResult(command.TransferId, false, 0, $"Merkez {(int)response.StatusCode} döndü.");

            partPath = fullPath + PartSuffix;
            if (File.Exists(partPath)) File.Delete(partPath);   // önceki yarım aktarım
            long bytes;
            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var part = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                // Açıldıktan sonra gerçek yol: denetimle açılış arasında araya giren bir bağ korumalı köke yönlendirmiş olabilir.
                if (FilePathPolicy.FinalPath(part.SafeFileHandle) is not { } final || _paths.IsProtected(final))
                {
                    _logger.LogWarning("Dosya transferi: gerçek yol korumalı ya da çözülemedi {Path}", partPath);
                    return new FileTransferResult(command.TransferId, false, 0, "Bu klasöre yazma kapalı (sistem klasörü).");
                }

                await remote.CopyToAsync(part, cancellationToken);
                await part.FlushAsync(cancellationToken);
                part.Flush(flushToDisk: true);
                bytes = part.Length;
            }

            if (response.Content.Headers.ContentLength is long expected && expected != bytes)
            {
                _logger.LogWarning("Dosya transferi: eksik aktarım {Path} ({Bytes}/{Expected} bayt)", fullPath, bytes, expected);
                return new FileTransferResult(command.TransferId, false, 0, "Aktarım eksik kaldı; dosya yazılmadı.");
            }

            File.Move(partPath, fullPath, overwrite: true);
            partPath = null;
            _logger.LogInformation("Dosya transferi: {Path} yazıldı ({Bytes} bayt)", fullPath, bytes);
            return new FileTransferResult(command.TransferId, true, bytes, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new FileTransferResult(command.TransferId, false, 0, "Hedef klasöre yazma yetkisi yok.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Dosya transferi: indirme başarısız {Dest}", command.DestinationPath);
            return new FileTransferResult(command.TransferId, false, 0, "Dosya yazılamadı.");
        }
        finally
        {
            // Başarısız / iptal: yarım parça silinir, hedefteki mevcut dosyaya hiç dokunulmamıştır.
            if (partPath is not null)
            {
                try { File.Delete(partPath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Dosya transferi: yarım parça silinemedi {Path}", partPath); }
            }
            EndActive();
        }
    }

    private string Base => _options.CentralApiUrl.Trim().TrimEnd('/');
    private string PushUrl(Guid id, string ticket) => $"{Base}/api/RemoteDesk/filetransfer/{id}/push?ticket={Uri.EscapeDataString(ticket)}";
    private string PullUrl(Guid id, string ticket) => $"{Base}/api/RemoteDesk/filetransfer/{id}/pull?ticket={Uri.EscapeDataString(ticket)}";

    private void BeginActive() { if (Interlocked.Increment(ref _active) == 1) ActiveChanged?.Invoke(true); }
    private void EndActive() { if (Interlocked.Decrement(ref _active) == 0) ActiveChanged?.Invoke(false); }

    private static string? ParentOf(DirectoryInfo dir) => dir.Parent?.FullName ?? "";   // "" = sürücü köklerine dön
    private static DateTime Modified(FileSystemInfo info) { try { return info.LastWriteTimeUtc; } catch { return DateTime.MinValue; } }
    private static long SafeLength(FileInfo file) { try { return file.Length; } catch { return 0; } }

    private static IEnumerable<T> SafeEnumerate<T>(Func<IEnumerable<T>> source)
    {
        IEnumerator<T> e;
        try { e = source().GetEnumerator(); }
        catch { yield break; }
        using (e)
        {
            while (true)
            {
                try { if (!e.MoveNext()) break; }
                catch { break; }   // erişilemeyen öğede dur
                yield return e.Current;
            }
        }
    }
}
