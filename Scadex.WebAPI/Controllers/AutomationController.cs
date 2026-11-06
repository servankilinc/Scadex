using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Model.Dtos.Automation.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;
using Scadex.Core.Utils.BaseRequestModels;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class AutomationController : BaseController
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationStatusService _automationStatusService;
    private readonly IChannelEventService _channelEventService;
    public AutomationController(ILogger<AutomationController> logger, IAutomationService automationService, IAutomationStatusService automationStatusService, IChannelEventService channelEventService) : base(logger)
    {
        _automationService = automationService;
        _automationStatusService = automationStatusService;
        _channelEventService = channelEventService;
    }

    #region Get
    /// <summary> Otomasyon durumunun dökümü (salt okunur): hangi cihaz/kamera otomasyona ne katkı veriyor. </summary>
    [HttpGet("{id:guid}/status-breakdown")]
    public async Task<IActionResult> GetStatusBreakdown(Guid id, CancellationToken cancellationToken)
    {
        var result = await _automationStatusService.GetBreakdownAsync(id, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Scada
    /// <summary>
    /// Diyagramdaki "Kartı oku": otomasyonun kartını okur (<c>GET {ScadaBaseUrl}/status</c>) ve farklı çıkan kanal değerlerini yazar; değişenler
    /// <c>DiagramHub</c>'dan tuvale yayınlanır, yanıt yalnızca özettir. <c>ViewDiagram</c>: sahaya komut gitmez, yalnızca telemetri tazelenir.
    /// </summary>
    [HttpPost("{id:guid}/scada-sync")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> SyncFromScada(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channelEventService.SyncFromScadaAsync(id, cancellationToken: cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _automationService.GetDetailListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    /// <summary> <c>ConfigureSystem</c>: otomasyon SCADA adresini taşır — tüm telemetri ve kumanda o adrese gider. </summary>
    [HttpPost]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Create(AutomationCreateDto request)
    {
        var result = await _automationService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    /// <summary> Düzenleme formunun modeli (SCADA adresi dahil); yalnızca düzenleyebilene açık. </summary>
    [HttpGet("{id:guid}/update")]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _automationService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    /// <summary> SCADA adresi ve bakım kararı da buradan yazılır. </summary>
    [HttpPut]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(AutomationUpdateDto request)
    {
        var result = await _automationService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion
}
