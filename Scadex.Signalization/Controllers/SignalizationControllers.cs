using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Dtos.Authority.Commands;
using Scadex.Signalization.Dtos.Config.Commands;
using Scadex.Signalization.Dtos.Operator.Commands;
using Scadex.Signalization.Dtos.Session.Queries;
using Scadex.Signalization.Services.Abstract;

namespace Scadex.Signalization.Controllers;

/// <summary> Kurumlar (ic kapi yetkisi tasiyan roller). </summary>
public class SignalAuthorityController : SignalizationControllerBase
{
    private readonly ISignalAuthorityService _service;
    public SignalAuthorityController(ILogger<SignalAuthorityController> logger, ISignalAuthorityService service) : base(logger) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => ToAction(await _service.GetAllAsync(cancellationToken));

    /// <summary> Tam liste; gövdede olmayan kurum pasife alinir. </summary>
    [HttpPut]
    public async Task<IActionResult> Save(SignalAuthoritySaveRequest request, CancellationToken cancellationToken) => ToAction(await _service.SaveAsync(request, cancellationToken));
}

/// <summary> Operatorler: kart numarasi ve tek kurum. </summary>
public class SignalOperatorController : SignalizationControllerBase
{
    private readonly ISignalOperatorService _service;
    public SignalOperatorController(ILogger<SignalOperatorController> logger, ISignalOperatorService service) : base(logger) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => ToAction(await _service.GetAllAsync(cancellationToken));

    [HttpPut("{userId:guid}/authority")]
    public async Task<IActionResult> SetAuthority(Guid userId, SignalOperatorAuthorityRequest request, CancellationToken cancellationToken)
        => ToAction(await _service.SetAuthorityAsync(userId, request, cancellationToken));
}

/// <summary> Kabin yapilandirmasi: kabin → dis kapilar → ic kapilar. </summary>
public class SignalCabinetController : SignalizationControllerBase
{
    private readonly ISignalCabinetService _service;
    public SignalCabinetController(ILogger<SignalCabinetController> logger, ISignalCabinetService service) : base(logger) => _service = service;

    [HttpGet("{cabinetId:guid}")]
    public async Task<IActionResult> Get(Guid cabinetId, CancellationToken cancellationToken) => ToAction(await _service.GetAsync(cabinetId, cancellationToken));

    [HttpGet("{cabinetId:guid}/options")]
    public async Task<IActionResult> GetOptions(Guid cabinetId, CancellationToken cancellationToken) => ToAction(await _service.GetOptionsAsync(cabinetId, cancellationToken));

    /// <summary> Tek yazim yolu; <c>cabinetId</c> route'tan gelir. </summary>
    [HttpPut("{cabinetId:guid}")]
    public async Task<IActionResult> Save(Guid cabinetId, SignalCabinetSaveRequest request, CancellationToken cancellationToken)
        => ToAction(await _service.SaveAsync(cabinetId, request, cancellationToken));
}

/// <summary> Operator islemleri: canli panel ve rapor (salt okunur). </summary>
public class OperatorSessionController : SignalizationControllerBase
{
    private readonly IOperatorSessionService _service;
    public OperatorSessionController(ILogger<OperatorSessionController> logger, IOperatorSessionService service) : base(logger) => _service = service;

    [HttpGet("open")]
    public async Task<IActionResult> GetOpen([FromQuery] Guid? cabinetId, CancellationToken cancellationToken) => ToAction(await _service.GetOpenAsync(cabinetId, cancellationToken));

    [HttpPost("list")]
    public async Task<IActionResult> List(OperatorSessionQueryRequest request, CancellationToken cancellationToken) => ToAction(await _service.GetPagedAsync(request, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken) => ToAction(await _service.GetDetailAsync(id, cancellationToken));

    [HttpPost("summary")]
    public async Task<IActionResult> Summary(OperatorSessionSummaryRequest request, CancellationToken cancellationToken) => ToAction(await _service.GetSummaryAsync(request, cancellationToken));
}
