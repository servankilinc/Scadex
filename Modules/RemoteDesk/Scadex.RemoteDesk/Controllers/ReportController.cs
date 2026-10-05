using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Controllers.Base;
using Scadex.RemoteDesk.Model.Dtos.Report.Queries;
using Scadex.RemoteDesk.Services.Abstract;

namespace Scadex.RemoteDesk.Controllers;

/// <summary>
/// PC izleme/kontrol denetim raporu — <c>RemotePcAudit</c> ister (izleme izninden ayrı). Listeler, Olay Geçmişi gibi gövdeli
/// <c>POST</c>'tur: tarih süzgeci JSON'da UTC olarak taşınır.
/// </summary>
[Authorize(Policy = RemoteDeskModule.AuditPolicy)]
public class ReportController : RemoteDeskControllerBase
{
    private readonly IRemoteAccessReportService _service;
    public ReportController(ILogger<ReportController> logger, IRemoteAccessReportService service) : base(logger) => _service = service;

    [HttpGet("reports/filters")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken cancellationToken)
    {
        var result = await _service.GetFilterOptionsAsync(cancellationToken);
        return ToAction(result);
    }

    [HttpPost("reports/views")]
    public async Task<IActionResult> GetViews(RemoteAccessReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetViewsAsync(query, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("reports/controls")]
    public async Task<IActionResult> GetControls(RemoteAccessReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetControlsAsync(query, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("reports/summary")]
    public async Task<IActionResult> GetSummary(RemoteAccessReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetSummaryAsync(query, cancellationToken);
        return ToAction(result);
    }
}
