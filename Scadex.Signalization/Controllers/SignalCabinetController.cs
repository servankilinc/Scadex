using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Config.Commands;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Controllers;

/// <summary> Kabin yapilandirmasi: kabin → dis kapilar → ic kapilar. </summary>
public class SignalCabinetController : SignalizationControllerBase
{
    private readonly ISignalCabinetService _service;
    public SignalCabinetController(ILogger<SignalCabinetController> logger, ISignalCabinetService service) : base(logger) => _service = service;


    [HttpGet("{cabinetId:guid}")]
    public async Task<IActionResult> Get(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    [HttpGet("{cabinetId:guid}/options")]
    public async Task<IActionResult> GetOptions(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetOptionsAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Tek yazim yolu; <c>cabinetId</c> route'tan gelir. </summary>
    [HttpPut("{cabinetId:guid}")]
    public async Task<IActionResult> Save(Guid cabinetId, SignalCabinetSaveRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Sanal kabin ekranı için gerekli bilgileri sağlar: kabinin durumu (kapilar, siren, aydinlatma, kilitler). </summary>
    [HttpGet("{cabinetId:guid}/live")]
    public async Task<IActionResult> GetLive(Guid cabinetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetLiveAsync(cabinetId, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Sanal kabin modülünden manuel komut gönderim metodu. </summary>
    [HttpPost("{cabinetId:guid}/command")]
    public async Task<IActionResult> SendCommand(Guid cabinetId, SignalCabinetCommandRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SendCommandAsync(cabinetId, request, cancellationToken);
        return ToAction(result);
    }
}
