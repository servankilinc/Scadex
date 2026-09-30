using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Controllers.Base;
using Scadex.RemoteDesk.Services.Abstract;

namespace Scadex.RemoteDesk.Controllers;

/// <summary> Ekran izleme: başlat, kiralamayı yenile, bırak. </summary>
public class ViewController : RemoteDeskControllerBase
{
    private readonly IScreenViewService _service;
    public ViewController(ILogger<ViewController> logger, IScreenViewService service) : base(logger) => _service = service;

    /// <summary> Yayın yoksa başlatır ve hazır olana kadar bekler (en fazla ~15 sn). </summary>
    [HttpPost("pcs/{deviceId:guid}/monitors/{index:int}/view")]
    public async Task<IActionResult> Start(Guid deviceId, int index, CancellationToken cancellationToken)
    {
        var result = await _service.StartAsync(deviceId, index, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Tarayıcı <c>leaseRenewSec</c>'te bir çağırır; 404 = izleme sona erdi. </summary>
    [HttpPost("views/{viewId:guid}/renew")]
    public async Task<IActionResult> Renew(Guid viewId, CancellationToken cancellationToken)
    {
        var result = await _service.RenewAsync(viewId, cancellationToken);
        return ToAction(result);
    }

    [HttpDelete("views/{viewId:guid}")]
    public async Task<IActionResult> Release(Guid viewId, CancellationToken cancellationToken)
    {
        var result = await _service.ReleaseAsync(viewId, cancellationToken);
        return ToAction(result);
    }
}
