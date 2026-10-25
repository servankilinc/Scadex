using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.FileTransfer;

/// <summary>
/// Dosya transferinin merkezdeki geçici deposu — SINGLETON, bellekte dizin + diskte geçici dosya. Baytlar hub'dan GEÇMEZ; tarayıcı
/// ile PC arasında merkez bu geçici dosya üzerinden köprü kurar.
/// <para/>
/// PC'nin HTTP uçları (<c>push</c>/<c>pull</c>) iki şey ister: PC'nin Basic Auth kimliği VE transferin bileti.
/// Bilet PC'ye komut gönderilirken üretilir (<see cref="IssueTicket"/>), <see cref="TicketTtl"/> içinde, YALNIZCA o transferin cihazınca ve
/// YALNIZCA BİR KEZ kullanılabilir (<see cref="TryConsumeTicket"/>) — ele geçirilen bilet başka cihazdan da, ikinci kez de işe yaramaz.
/// Geçici dosyalar transfer bitince ya da <see cref="EntryTtl"/> dolunca silinir.
/// </summary>
public sealed class FileTransferStore : IDisposable
{
    /// <summary> Dosya başına üst sınır (kullanıcı kararı): 1 GB. Akış sırasında zorlanır. </summary>
    public const long MaxBytes = 1L * 1024 * 1024 * 1024;

    /// <summary> Bağlanmamış/yarım kalan transferin yaşam süresi; süpürmede silinir. </summary>
    public static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Biletin PC'ye verildikten sonra kullanılabileceği süre. İsteğin BAŞLAMASI içindir; başlamış aktarım sürer. PC aktarımı yeniden
    /// denemez, bu yüzden kısa ömür ve tek kullanım güvenle uygulanır.
    /// </summary>
    public static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Geçici klasörde sahipsiz dosya taramasının aralığı. İlk tarama açılıştan sonraki ilk süpürmededir: yeniden başlamada
    /// bellekteki kayıtlar gider, diskteki dosyalar kalırdı.
    /// </summary>
    public static readonly TimeSpan OrphanScanInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Sahipsiz dosyanın silinmeden önceki son-yazım yaşı. "Açılışta klasörü boşalt" bilerek YAPILMAZ: IIS havuz geri dönüşümünde yeni süreç,
    /// eski süreç birkaç saniye daha aktarım sürdürürken açılır; o sürecin yazdığı dosya kayıtlarımızda yoktur ama sürekli tazelenir. Bekleyen
    /// dosyası ise o süreç bitince zaten ölüdür.
    /// </summary>
    public static readonly TimeSpan OrphanGrace = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, FileTransferEntry> _entries = new();
    private readonly Lock _ticketGate = new();
    private readonly string _tempRoot;
    private readonly ILogger<FileTransferStore> _logger;
    private DateTime _nextOrphanScanUtc = DateTime.MinValue;

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
            FileName = PcPathText.SafeFileName(fileName),
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
            FileName = PcPathText.SafeFileName(pcPath),
            SourcePath = pcPath,
            TempPath = Path.Combine(_tempRoot, id.ToString("N")),
            Status = FileTransferStatus.PcSending,
            CreatedUtc = DateTime.UtcNow
        };
        _entries[id] = entry;
        return entry;
    }

    public bool TryGet(Guid transferId, out FileTransferEntry entry) => _entries.TryGetValue(transferId, out entry!);

    /// <summary>
    /// PC'ye komut gönderilirken transferin biletini üretir (önceki bilet varsa geçersiz olur). Transfer yoksa <c>null</c>.
    /// </summary>
    public string? IssueTicket(Guid transferId)
    {
        if (!_entries.TryGetValue(transferId, out var e)) return null;
        lock (_ticketGate)
        {
            e.Ticket = NewTicket();
            e.TicketExpiresUtc = DateTime.UtcNow + TicketTtl;
            return e.Ticket;
        }
    }

    /// <summary>
    /// PC'nin <c>push</c>/<c>pull</c> isteği: bilet bu transferin, bu yönün ve bu CİHAZIN bileti mi, süresi dolmadı mı — öyleyse atomik olarak
    /// tüketir (ikinci istek reddedilir). <paramref name="reason"/> yalnızca günlük içindir.
    /// </summary>
    public bool TryConsumeTicket(Guid transferId, string? ticket, Guid deviceId, FileTransferDirection direction,
        out FileTransferEntry entry, out string reason)
    {
        entry = null!;
        if (string.IsNullOrEmpty(ticket)) { reason = "bilet yok"; return false; }
        if (!_entries.TryGetValue(transferId, out var e)) { reason = "transfer yok"; return false; }
        if (e.Direction != direction) { reason = "yön uymuyor"; return false; }
        if (e.DeviceId != deviceId) { reason = "transfer başka cihazın"; return false; }

        lock (_ticketGate)
        {
            if (e.Ticket is null) { reason = "bilet kullanılmış ya da verilmemiş"; return false; }
            if (DateTime.UtcNow > e.TicketExpiresUtc) { reason = "biletin süresi dolmuş"; return false; }
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(e.Ticket), Encoding.UTF8.GetBytes(ticket)))
            {
                reason = "bilet eşleşmedi";
                return false;
            }
            e.Ticket = null;   // tek kullanım
        }

        entry = e;
        reason = "";
        return true;
    }

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

    /// <summary> Açık transferlerin anlık kopyası (izleme süpürmesi için). </summary>
    public IReadOnlyList<FileTransferEntry> Snapshot() => [.. _entries.Values];

    /// <summary> Süresi dolmuş/yarım kalanları ve diskteki sahipsiz geçici dosyaları süpür (ScreenStreamWorker turunda). </summary>
    public void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var e in _entries.Values)
            if (now - e.CreatedUtc > EntryTtl)
                Remove(e.TransferId);

        if (now >= _nextOrphanScanUtc)
        {
            _nextOrphanScanUtc = now + OrphanScanInterval;
            SweepOrphans(now);
        }
    }

    /// <summary>
    /// Kaydı olmayan ve <see cref="OrphanGrace"/>'ten eski geçici dosyaları siler: yeniden başlamadan kalanlar ve
    /// <see cref="Remove"/>'un o an silemedikleri. Tarayıcıya akmakta olan indirme (<c>Forget</c> + <c>DeleteOnClose</c>) kayıtta yoktur ama
    /// açıktır; silme paylaşım ihlaliyle reddedilir ve atlanır, dosya kapanınca kendi gider.
    /// </summary>
    private void SweepOrphans(DateTime now)
    {
        try
        {
            var owned = _entries.Values.Select(e => e.TempPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            long bytes = 0;
            foreach (var file in new DirectoryInfo(_tempRoot).EnumerateFiles())
            {
                if (owned.Contains(file.FullName) || now - file.LastWriteTimeUtc < OrphanGrace)
                    continue;
                try
                {
                    long length = file.Length;
                    file.Delete();
                    count++;
                    bytes += length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug(ex, "RemoteDesk: sahipsiz geçici dosya şimdilik silinemedi {Path}", file.FullName);
                }
            }
            if (count > 0)
                _logger.LogInformation("RemoteDesk: sahipsiz {Count} geçici transfer dosyası silindi ({Megabytes:0.#} MB)", count, bytes / 1024d / 1024d);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "RemoteDesk: geçici transfer klasörü taranamadı {Path}", _tempRoot);
        }
    }

    /// <summary>
    /// Gövdeyi geçici dosyaya akıtır; <see cref="MaxBytes"/> aşılırsa -1 döner ve dosyayı siler. Tarayıcı yüklemesi ve PC'nin <c>push</c>'u
    /// aynı sınırı kullanır.
    /// </summary>
    public static async Task<long> CopyWithLimitAsync(Stream source, string destPath, CancellationToken cancellationToken)
    {
        long total = 0;
        var buffer = new byte[64 * 1024];
        // FileShare.Delete: izleme süpürmesi (Remove) yazılırken dosyayı silebilsin — Windows'ta kapanınca gider, aksi hâlde yetim kalırdı.
        await using (var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.Delete, buffer.Length, FileOptions.Asynchronous))
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxBytes)
                {
                    dest.Close();
                    try { File.Delete(destPath); } catch { /* yok say */ }
                    return -1;
                }
                await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        return total;
    }

    private void TryDeleteTemp(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "RemoteDesk: geçici transfer dosyası silinemedi {Path}", path); }
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
    /// <summary> PC'nin tek kullanımlık bileti; <c>null</c> = henüz verilmedi ya da kullanıldı (<see cref="FileTransferStore.TryConsumeTicket"/>). </summary>
    public string? Ticket { get; set; }
    public DateTime TicketExpiresUtc { get; set; }
    public string TempPath { get; init; } = "";
    public long Bytes { get; set; }
    public string? Error { get; set; }
    public FileTransferStatus Status { get; set; }
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// PC ayağının (<c>push</c>/<c>pull</c>) akışını keser: kullanıcı PC'yi izlemeyi bırakınca süpürme iptal eder (<see cref="FileTransferCoordinator.SweepAsync"/>). 
    /// Bilerek dispose EDİLMEZ: zamanlayıcısız CTS kaynak tutmaz, kayıt düşerken aynı anda token'ı alan bir <c>push</c>/<c>pull</c> da <c>ObjectDisposedException</c> almasın.
    /// </summary>
    public CancellationTokenSource Abort { get; } = new();
}
