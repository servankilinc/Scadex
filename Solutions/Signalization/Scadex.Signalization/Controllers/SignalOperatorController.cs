using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.Auth;
using Scadex.Signalization.Controllers.Base;
using Scadex.Signalization.Model.Dtos.Operator.Commands;
using Scadex.Signalization.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

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

    /// <summary> Operatörün kartının hangi iç kapıları açacağını belirler — geçiş yetkisi (<c>ManageAccessCards</c>). </summary>
    [HttpPut("{userId:guid}/authority")]
    [RequirePermission(Permission.ManageAccessCards)]
    public async Task<IActionResult> SetAuthority(Guid userId, SignalOperatorAuthorityRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SetAuthorityAsync(userId, request, cancellationToken);
        return ToAction(result);
    }
}
