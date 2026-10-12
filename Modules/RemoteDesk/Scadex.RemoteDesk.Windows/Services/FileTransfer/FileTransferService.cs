using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.RemoteDesk.Contracts.Hub;

namespace Scadex.RemoteDesk.Windows.Services.FileTransfer;

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
/// biletli HTTP üzerinden aktarılır (<c>{CentralApiUrl}/api/RemoteDesk/filetransfer/{id}/push|pull?ticket=</c>). İstemci yükseltilmiş
/// çalışır; YAZMA (indirme) öncesi hedef yol normalize edilip korumalı köklere (Windows / Program Files / Startup) karşı denetlenir.
/// </summary>
public sealed class FileTransferService : IFileTransferService
{
    private const int MaxEntries = 5000;

    /// <summary> Yüklemenin (PC'ye yazmanın) yasak olduğu kökler (kullanıcı kararı). Gezme/okuma/indirme serbest. </summary>
    private static readonly string[] ProtectedRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
    ];

    private readonly RemoteDeskClientOptions _options;
    private readonly ILogger<FileTransferService> _logger;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private int _active;

    public FileTransferService(IOptions<RemoteDeskClientOptions> options, ILogger<FileTransferService> logger)
    {
        _options = options.Value;
        _logger = logger;
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
        try
        {
            string fullPath;
            try { fullPath = Path.GetFullPath(Path.Combine(command.DestinationPath, command.FileName)); }
            catch { return new FileTransferResult(command.TransferId, false, 0, "Geçersiz hedef yol."); }

            if (IsProtected(fullPath))
                return new FileTransferResult(command.TransferId, false, 0, "Bu klasöre yazma kapalı (sistem klasörü).");
            if (!Directory.Exists(command.DestinationPath))
                return new FileTransferResult(command.TransferId, false, 0, "Hedef klasör yok.");

            using var response = await _http.GetAsync(PullUrl(command.TransferId, command.Ticket), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new FileTransferResult(command.TransferId, false, 0, $"Merkez {(int)response.StatusCode} döndü.");

            await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var dest = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
            await remote.CopyToAsync(dest, cancellationToken);
            long bytes = dest.Length;

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
        finally { EndActive(); }
    }

    private static bool IsProtected(string fullPath)
    {
        foreach (var root in ProtectedRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            string r = root.TrimEnd('\\', '/');
            if (fullPath.Equals(r, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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
