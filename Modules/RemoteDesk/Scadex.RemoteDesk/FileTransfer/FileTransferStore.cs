using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.FileTransfer;

/// <summary>
/// Dosya transferinin merkezdeki geçici deposu — SINGLETON, bellekte dizin + diskte geçici dosya. Baytlar hub'dan GEÇMEZ; tarayıcı
/// ile PC arasında merkez bu geçici dosya üzerinden köprü kurar. Her transfer tek-kullanımlık bir <see cref="FileTransferEntry.Ticket"/>
/// taşır: PC'nin (JWT'siz) HTTP uçlarına erişimini yetkilendirir (biletli HTTP, <see cref="Media.ScreenTicketStore"/> kalıbı).
/// Geçici dosyalar transfer bitince ya da <see cref="EntryTtl"/> dolunca silinir.
/// </summary>
public sealed class FileTransferStore : IDisposable
{
    /// <summary> Dosya başına üst sınır (kullanıcı kararı): 1 GB. Akış sırasında zorlanır. </summary>
    public const long MaxBytes = 1L * 1024 * 1024 * 1024;

    /// <summary> Bağlanmamış/yarım kalan transferin yaşam süresi; süpürmede silinir. </summary>
    public static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<Guid, FileTransferEntry> _entries = new();
    private readonly string _tempRoot;
    private readonly ILogger<FileTransferStore> _logger;

    public FileTransferStore(ILogger<FileTransferStore> logger)
    {
        _logger = logger;
        _tempRoot = Path.Combine(Path.GetTempPath(), "scadex-remotedesk-ft");
        Directory.CreateDirectory(_tempRoot);
    }

    /// <summary> Tarayıcı → PC: tarayıcı dosyayı merkeze yüklerken kayıt açılır; baytlar <paramref name="tempPath"/>'e yazılır. </summary>
    public FileTransferEntry BeginBrowserUpload(Guid userId, string userName, string fileName)
    {
        var id = Guid.NewGuid();
        var entry = new FileTransferEntry
        {
            TransferId = id,
            Direction = FileTransferDirection.ToPc,
            UserId = userId,
            UserName = userName,
            FileName = SafeFileName(fileName),
            Ticket = NewTicket(),
            TempPath = Path.Combine(_tempRoot, id.ToString("N")),
            Status = FileTransferStatus.CentralReceiving,
            CreatedUtc = DateTime.UtcNow
        };
        _entries[id] = entry;
        return entry;
    }

    /// <summary> PC → tarayıcı: PC, dosyayı merkeze yükleyecek; kayıt açılır, PC bilet ile <c>PUT …/push</c> yapar. </summary>
    public FileTransferEntry BeginPcUpload(Guid deviceId, Guid userId, string userName, string pcPath)
    {
        var id = Guid.NewGuid();
        var entry = new FileTransferEntry
        {
            TransferId = id,
            Direction = FileTransferDirection.FromPc,
            DeviceId = deviceId,
            UserId = userId,
            UserName = userName,
            FileName = SafeFileName(Path.GetFileName(pcPath.TrimEnd('\\', '/'))),
            SourcePath = pcPath,
            Ticket = NewTicket(),
            TempPath = Path.Combine(_tempRoot, id.ToString("N")),
            Status = FileTransferStatus.PcSending,
            CreatedUtc = DateTime.UtcNow
        };
        _entries[id] = entry;
        return entry;
    }

    public bool TryGet(Guid transferId, out FileTransferEntry entry) => _entries.TryGetValue(transferId, out entry!);

    /// <summary> Bilet bu transferin bileti mi (PC HTTP uçları için). </summary>
    public bool ValidateTicket(Guid transferId, string? ticket) =>
        !string.IsNullOrEmpty(ticket) && _entries.TryGetValue(transferId, out var e) && e.Ticket == ticket;

    public void SetStatus(Guid transferId, FileTransferStatus status, long bytes = -1, string? error = null)
    {
        if (!_entries.TryGetValue(transferId, out var e)) return;
        e.Status = status;
        if (bytes >= 0) e.Bytes = bytes;
        if (error is not null) e.Error = error;
    }

    /// <summary> Tarayıcı→PC transferini hedef yola bağlar (RequestUpload). </summary>
    public void BindDestination(Guid transferId, Guid deviceId, string destinationPath)
    {
        if (_entries.TryGetValue(transferId, out var e))
        {
            e.DeviceId = deviceId;
            e.DestinationPath = destinationPath;
        }
    }

    /// <summary> Transferi kapatır ve geçici dosyasını siler. </summary>
    public void Remove(Guid transferId)
    {
        if (_entries.TryRemove(transferId, out var e))
            TryDeleteTemp(e.TempPath);
    }

    /// <summary> Kaydı düşürür ama geçici dosyaya DOKUNMAZ (dosya akışı <c>DeleteOnClose</c> ile kendini siler — indirme sırasında). </summary>
    public void Forget(Guid transferId) => _entries.TryRemove(transferId, out _);

    /// <summary> Süresi dolmuş/yarım kalanları süpür (ScreenStreamWorker turunda). </summary>
    public void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var e in _entries.Values)
            if (now - e.CreatedUtc > EntryTtl)
                Remove(e.TransferId);
    }

    private void TryDeleteTemp(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "RemoteDesk: geçici transfer dosyası silinemedi {Path}", path); }
    }

    private static string SafeFileName(string name)
    {
        name = Path.GetFileName(name);
        return string.IsNullOrWhiteSpace(name) ? "dosya" : string.Concat(name.Split(Path.GetInvalidFileNameChars()));
    }

    private static string NewTicket() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public void Dispose()
    {
        foreach (var e in _entries.Values) TryDeleteTemp(e.TempPath);
        _entries.Clear();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { /* yok say */ }
    }
}

/// <summary> Tek bir transferin merkezdeki durumu (bellekte). </summary>
public sealed class FileTransferEntry
{
    public Guid TransferId { get; init; }
    public FileTransferDirection Direction { get; init; }
    public Guid? DeviceId { get; set; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = "";
    /// <summary> Durum bildirimini alacak tarayıcı bağlantısı (istek anında bağlanır). </summary>
    public string? ViewerConnectionId { get; set; }
    public string FileName { get; init; } = "";
    /// <summary> FromPc: okunacak PC yolu. </summary>
    public string? SourcePath { get; init; }
    /// <summary> ToPc: yazılacak PC yolu (RequestUpload'da bağlanır). </summary>
    public string? DestinationPath { get; set; }
    public string Ticket { get; init; } = "";
    public string TempPath { get; init; } = "";
    public long Bytes { get; set; }
    public string? Error { get; set; }
    public FileTransferStatus Status { get; set; }
    public DateTime CreatedUtc { get; init; }
}
