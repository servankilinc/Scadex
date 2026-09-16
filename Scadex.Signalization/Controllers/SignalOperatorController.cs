using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Operator.Commands;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Controllers;

/// <summary> Operatorler: kart numarasi ve tek kurum. </summary>
public class SignalOperatorController : SignalizationControllerBase
{
    private readonly ISignalOperatorService _service;
    public SignalOperatorController(ILogger<SignalOperatorController> logger, ISignalOperatorService service) : base(logger) => _service = service;


    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return ToAction(result);
    }

    [HttpPut("{userId:guid}/authority")]
    public async Task<IActionResult> SetAuthority(Guid userId, SignalOperatorAuthorityRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SetAuthorityAsync(userId, request, cancellationToken);
        return ToAction(result);
    }
}
