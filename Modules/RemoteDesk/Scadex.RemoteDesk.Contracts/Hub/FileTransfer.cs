namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary>
/// Dosya transferi sözleşmesi (tarayıcı ↔ merkez ↔ uzak PC). Dosyanın BAYTLARI hub'dan geçmez; yalnızca komut ve küçük dizin listesi
/// hub'dan gider, baytlar tek-kullanımlık biletli HTTP ile merkez üzerinden akar (dosya transferi). Yalnızca o an
/// PC'nin kontrolünü elinde tutan kullanıcı tetikleyebilir; her yükleme/indirme denetim kaydına yazılır.
/// </summary>

/// <summary> PC'de bir klasörü listele. <c>Path</c> boş/null ise sürücü köklerini döndür. </summary>
public sealed record ListDirectoryCommand(string? Path);

/// <summary> Bir klasör ya da dosya girdisi. </summary>
public sealed record DirEntry(string Name, bool IsDirectory, long Size, DateTime ModifiedUtc);

/// <summary>
/// Dizin listesi yanıtı. <c>Error</c> doluysa (erişim reddi vb.) <c>Entries</c> boştur ve kullanıcıya gösterilir.
/// </summary>
/// <param name="IsRoot">Sürücü kökleri listesi mi (üst klasör yok).</param>
/// <param name="ParentPath">Bir üst klasör; kökte <c>null</c>.</param>
public sealed record DirectoryListing(string Path, bool IsRoot, string? ParentPath, DirEntry[] Entries, string? Error);

/// <summary>
/// PC → merkeze yükle (indirme yönü): PC, <c>Path</c>'teki dosyayı okuyup <c>PUT /api/RemoteDesk/filetransfer/{TransferId}</c> ile merkeze
/// akıtır (<c>Ticket</c> parola alanında). Merkez tarayıcıya indirtir.
/// </summary>
public sealed record FileUploadCommand(Guid TransferId, string Path, string Ticket);

/// <summary>
/// Merkezden PC'ye indir (yükleme yönü): PC <c>GET /api/RemoteDesk/filetransfer/{TransferId}</c> ile çeker ve <c>DestinationPath</c>'e
/// (varsa <c>FileName</c> ile birleştirip) yazar. İstemci yükseltilmiş çalışır; yazmadan önce yolu normalize edip korumalı köklere
/// (Windows / Program Files / Startup) karşı denetler, reddederse <see cref="FileTransferResult"/> ile bildirir.
/// </summary>
public sealed record FileDownloadCommand(Guid TransferId, string DestinationPath, string FileName, string Ticket);

/// <summary> PC → merkez: bir transferin sonucu. </summary>
public sealed record FileTransferResult(Guid TransferId, bool Success, long Bytes, string? Error);
