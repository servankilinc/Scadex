using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class DiagramController : BaseController
{
    private readonly IDiagramService _diagramService;

    public DiagramController(ILogger<DiagramController> logger, IDiagramService diagramService) : base(logger)
        => _diagramService = diagramService;


    [HttpGet("cabinet/{cabinetId:guid}")]
    public async Task<IActionResult> GetCabinetDiagram(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _diagramService.GetAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("cabinet/{cabinetId:guid}/save")]
    public async Task<IActionResult> Save(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.SaveAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }
}
