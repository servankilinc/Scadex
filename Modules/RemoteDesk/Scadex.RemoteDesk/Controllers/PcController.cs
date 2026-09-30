using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Controllers.Base;
using Scadex.RemoteDesk.Services.Abstract;

namespace Scadex.RemoteDesk.Controllers;

/// <summary> İzlenebilir PC'ler. </summary>
public class PcController : RemoteDeskControllerBase
{
    private readonly IPcDirectoryService _service;
    public PcController(ILogger<PcController> logger, IPcDirectoryService service) : base(logger) => _service = service;

    [HttpGet("pcs")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return ToAction(result);
    }
}
