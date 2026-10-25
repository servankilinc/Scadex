using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class DiagramController : BaseController
{
    private readonly IDiagramService _diagramService;
    private readonly ICameraService _cameraService;

    public DiagramController(ILogger<DiagramController> logger, IDiagramService diagramService, ICameraService cameraService) : base(logger)
    {
        _diagramService = diagramService;
        _cameraService = cameraService;
    }


    [HttpGet("cabinet/{cabinetId:guid}")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> GetCabinetDiagram(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _diagramService.GetAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Diyagram tablolarının tek yazım yolu. </summary>
    [HttpPost("cabinet/{cabinetId:guid}/save")]
    [RequirePermission(Permission.EditDiagram)]
    public async Task<IActionResult> Save(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var result = await _diagramService.SaveAsync(cabinetId, request, cancellationToken);

        // Commit SONRASI: kamera bilgisi degisen ya da silinen kameralarin MediaMTX yollari dusurulur, acik izleyicinin eski
        // bilgilerle kurulan oturumu kesilir. Kaydetmenin icinde degil burada, cunku DiagramService.Save.cs'e dokunulmaz ve
        // yol, geri alinabilecek bir transaction'dan once silinmemeli. Basarisizlik yalnizca loglanir.
        if (result.IsSuccess)
        {
            var touchedDeviceIds = request.Devices.Upserted.Select(d => d.Id).Concat(request.Devices.Deleted).Distinct().ToList();
            await _cameraService.RefreshStreamPathsAsync(touchedDeviceIds, cancellationToken);
        }

        return ToAction(result);
    }
}
