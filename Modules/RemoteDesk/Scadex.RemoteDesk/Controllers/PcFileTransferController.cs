using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Scadex.Business.Utils.Auth;
using Scadex.RemoteDesk.Controllers.Base;
using Scadex.RemoteDesk.FileTransfer;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Controllers;

/// <summary>
/// Dosya transferinin PC tarafı BAYT yüzeyi — Agent buraya bağlanır, tarayıcı değil. İki şart birden:
/// <list type="bullet">
/// <item>PC'nin Basic Auth kimliği (<see cref="DeviceAuthPolicies.Pc"/>, <c>PcHub</c> ile aynı) — kullanıcı JWT'si geçmez.</item>
/// <item>Transferin bileti: o transferin, o yönün ve o CİHAZIN bileti, verildikten sonra 2 dk içinde, tek kullanımlık
/// (<see cref="FileTransferStore.TryConsumeTicket"/>).</item>
/// </list>
/// Red 401'dir; neden yalnızca günlüğe yazılır, bilet hiç yazılmaz. Yollar eskisiyle aynıdır (PC istemcisinin sözleşmesi değişmedi).
/// </summary>
[Authorize(Policy = DeviceAuthPolicies.Pc)]
public class PcFileTransferController : RemoteDeskControllerBase
{
    private readonly FileTransferStore _store;
    private readonly ILogger<PcFileTransferController> _logger;

    public PcFileTransferController(ILogger<PcFileTransferController> logger, FileTransferStore store) : base(logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary> PC → merkeze yükle (FromPc baytları). </summary>
    [HttpPut("filetransfer/{transferId:guid}/push")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Push(Guid transferId, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        if (!Consume(transferId, ticket, FileTransferDirection.FromPc, out var entry))
            return Unauthorized();

        // Kullanıcı izlemeyi bırakınca süpürme entry.Abort'u iptal eder (readiness P1.3): akış kesilir, yarım geçici dosya silinir.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, entry.Abort.Token);
        long bytes;
        try
        {
            bytes = await FileTransferStore.CopyWithLimitAsync(Request.Body, entry.TempPath, linked.Token);
        }
        catch (OperationCanceledException) when (entry.Abort.IsCancellationRequested)
        {
            DeleteTemp(entry.TempPath);
            HttpContext.Abort();
            return new EmptyResult();
        }
        if (bytes < 0)
        {
            _store.SetStatus(transferId, FileTransferStatus.Failed, error: "1 GB sınırı aşıldı");
            return BadRequest();
        }
        _store.SetStatus(transferId, entry.Status, bytes);   // durum ReportFileTransfer ile ilerler
        return Ok();
    }

    /// <summary>
    /// PC → merkezden çek (ToPc baytları). <c>File(stream)</c> yerine elle akıtılır: kullanıcı izlemeyi bırakınca akış ortasında kesilebilsin
    /// Kesilen yanıt <c>Content-Length</c>'ten kısa kalır; PC yarım parçayı siler, hedefe hiçbir şey yazılmaz.
    /// </summary>
    [HttpGet("filetransfer/{transferId:guid}/pull")]
    public async Task Pull(Guid transferId, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        if (!Consume(transferId, ticket, FileTransferDirection.ToPc, out var entry))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (!System.IO.File.Exists(entry.TempPath))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, entry.Abort.Token);
        await using var stream = new FileStream(entry.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous);
        Response.ContentType = "application/octet-stream";
        Response.ContentLength = stream.Length;
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = entry.FileName }.ToString();
        try
        {
            await stream.CopyToAsync(Response.Body, 64 * 1024, linked.Token);
        }
        catch (OperationCanceledException) when (entry.Abort.IsCancellationRequested)
        {
            HttpContext.Abort();
        }
    }

    private void DeleteTemp(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "RemoteDesk: kesilen transferin geçici dosyası silinemedi {Path}", path); }
    }

    private bool Consume(Guid transferId, string? ticket, FileTransferDirection direction, out FileTransferEntry entry)
    {
        // Policy cihaz kimliğini zorladı; yine de yoksa reddedilir.
        if (User.GetDeviceId() is not { } deviceId)
        {
            entry = null!;
            return false;
        }
        if (_store.TryConsumeTicket(transferId, ticket, deviceId, direction, out entry, out string reason))
            return true;

        _logger.LogWarning("RemoteDesk: dosya transferi bileti reddedildi — transfer {TransferId}, cihaz {DeviceId}, {Ip}: {Reason}",
            transferId, deviceId, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?", reason);
        return false;
    }
}
