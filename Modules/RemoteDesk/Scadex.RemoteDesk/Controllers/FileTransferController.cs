using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Controllers.Base;
using Scadex.RemoteDesk.FileTransfer;
using Scadex.RemoteDesk.Model.Dtos.FileTransfer;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Controllers;

/// <summary>
/// Dosya transferinin BAYT yüzeyi. Baytlar hub'dan değil buradan, merkezdeki geçici dosya üzerinden akar.
/// Tarayıcı uçları <c>RemotePcFileTransfer</c> ister (JWT); PC uçları (<c>pull</c>/<c>push</c>) JWT'siz, tek-kullanımlık BİLETLE
/// yetkilendirilir. 1 GB sınırı akışta zorlanır. Hangi dosyanın nereye gideceği viewer hub'ında kontrol sahipliğiyle bağlanır.
/// </summary>
[Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
public class FileTransferController : RemoteDeskControllerBase
{
    private readonly FileTransferStore _store;
    private readonly ILogger<FileTransferController> _logger;

    public FileTransferController(ILogger<FileTransferController> logger, FileTransferStore store) : base(logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary> Tarayıcı → merkez: dosyayı yükle (gövde = ham baytlar, ad <c>fileName</c> sorgusunda). Döner: transferId. Sonra viewer hub RequestUpload. </summary>
    [HttpPost("filetransfer")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = FileTransferStore.MaxBytes)]
    public async Task<IActionResult> Upload([FromQuery] string fileName, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
            return Forbid();
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new FileTransferResponse { Accepted = false, Message = "Dosya adı boş." });

        string userName = User.FindFirstValue(ClaimTypes.Name) ?? userId.ToString();
        var entry = _store.BeginBrowserUpload(userId, userName, fileName);
        try
        {
            long bytes = await CopyWithLimitAsync(Request.Body, entry.TempPath, cancellationToken);
            if (bytes < 0)
            {
                _store.Remove(entry.TransferId);
                return BadRequest(new FileTransferResponse { Accepted = false, Message = "Dosya 1 GB sınırını aşıyor." });
            }
            _store.SetStatus(entry.TransferId, FileTransferStatus.ReadyForPc, bytes);
            return Ok(new FileTransferResponse { Accepted = true, TransferId = entry.TransferId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _store.Remove(entry.TransferId);
            _logger.LogWarning(ex, "RemoteDesk: tarayıcı yüklemesi başarısız {TransferId}", entry.TransferId);
            return StatusCode(500, new FileTransferResponse { Accepted = false, Message = "Yükleme başarısız." });
        }
    }

    /// <summary> Tarayıcı → merkezden indir (PC'den gelmiş dosya hazırsa). Sahiplik + hazır durum şart. </summary>
    [HttpGet("filetransfer/{transferId:guid}")]
    public IActionResult Download(Guid transferId)
    {
        if (CurrentUserId() is not { } userId)
            return Forbid();
        if (!_store.TryGet(transferId, out var entry) || entry.UserId != userId
            || entry.Direction != FileTransferDirection.FromPc || entry.Status != FileTransferStatus.ReadyForBrowser
            || !System.IO.File.Exists(entry.TempPath))
            return NotFound();

        // İndirme bitince geçici dosya ve kayıt silinir (FileStream kapanınca). FileStreamResult akışı sahiplenir.
        var stream = new FileStream(entry.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        _store.Forget(transferId);   // kaydı düşür; dosya DeleteOnClose ile gider
        return File(stream, "application/octet-stream", entry.FileName);
    }

    /// <summary> PC → merkeze yükle (FromPc baytları). JWT'siz; bilet şart. </summary>
    [AllowAnonymous]
    [HttpPut("filetransfer/{transferId:guid}/push")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Push(Guid transferId, [FromQuery] string ticket, CancellationToken cancellationToken)
    {
        if (!_store.ValidateTicket(transferId, ticket) || !_store.TryGet(transferId, out var entry) || entry.Direction != FileTransferDirection.FromPc)
            return Unauthorized();
        long bytes = await CopyWithLimitAsync(Request.Body, entry.TempPath, cancellationToken);
        if (bytes < 0)
        {
            _store.SetStatus(transferId, FileTransferStatus.Failed, error: "1 GB sınırı aşıldı");
            return BadRequest();
        }
        _store.SetStatus(transferId, entry.Status, bytes);   // durum ReportFileTransfer ile ilerler
        return Ok();
    }

    /// <summary> PC → merkezden çek (ToPc baytları). JWT'siz; bilet şart. </summary>
    [AllowAnonymous]
    [HttpGet("filetransfer/{transferId:guid}/pull")]
    public IActionResult Pull(Guid transferId, [FromQuery] string ticket)
    {
        if (!_store.ValidateTicket(transferId, ticket) || !_store.TryGet(transferId, out var entry)
            || entry.Direction != FileTransferDirection.ToPc || !System.IO.File.Exists(entry.TempPath))
            return Unauthorized();
        var stream = new FileStream(entry.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        return File(stream, "application/octet-stream", entry.FileName);
    }

    /// <summary> Gövdeyi dosyaya akıtır; <see cref="FileTransferStore.MaxBytes"/> aşılırsa -1 döner (ve dosyayı siler). </summary>
    private static async Task<long> CopyWithLimitAsync(Stream source, string destPath, CancellationToken cancellationToken)
    {
        long total = 0;
        var buffer = new byte[64 * 1024];
        await using (var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.Asynchronous))
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > FileTransferStore.MaxBytes)
                {
                    dest.Close();
                    try { System.IO.File.Delete(destPath); } catch { /* yok say */ }
                    return -1;
                }
                await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        return total;
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
