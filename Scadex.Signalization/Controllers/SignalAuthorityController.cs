using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Authority.Commands;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Controllers;

/// <summary> Kurumlar (ic kapi yetkisi tasiyan roller). </summary>
public class SignalAuthorityController : SignalizationControllerBase
{
    private readonly ISignalAuthorityService _service;
    public SignalAuthorityController(ILogger<SignalAuthorityController> logger, ISignalAuthorityService service) : base(logger) => _service = service;


    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return ToAction(result);
    }

    /// <summary> Tam liste; gövdede olmayan kurum pasife alinir. </summary>
    [HttpPut]
    public async Task<IActionResult> Save(SignalAuthoritySaveRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveAsync(request, cancellationToken);
        return ToAction(result);
    }
}
