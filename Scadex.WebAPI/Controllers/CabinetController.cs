using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.Auth;
using Scadex.Core.BaseRequestModels;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.WebAPI.Controllers.Base;
using Scadex.Business.Utils.RateLimiting;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class CabinetController : BaseController
{
    private readonly ICabinetService _cabinetService;
    private readonly ICabinetStatusService _cabinetStatusService;
    private readonly IChannelEventService _channelEventService;
    public CabinetController(ILogger<CabinetController> logger, ICabinetService cabinetService, ICabinetStatusService cabinetStatusService, IChannelEventService channelEventService) : base(logger)
    {
        _cabinetService = cabinetService;
        _cabinetStatusService = cabinetStatusService;
        _channelEventService = channelEventService;
    }

    #region Get
    /// <summary> Kabin durumunun dökümü (salt okunur): hangi cihaz/kamera kabine ne katkı veriyor. </summary>
    [HttpGet("{id:guid}/status-breakdown")]
    public async Task<IActionResult> GetStatusBreakdown(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cabinetStatusService.GetBreakdownAsync(id, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region Scada
    /// <summary>
    /// Diyagramdaki "Kartı oku": kabinin kartını okur (<c>GET {ScadaBaseUrl}/status</c>) ve farklı çıkan kanal değerlerini yazar; değişenler
    /// <c>DiagramHub</c>'dan tuvale yayınlanır, yanıt yalnızca özettir. <c>ViewDiagram</c>: sahaya komut gitmez, yalnızca telemetri tazelenir.
    /// </summary>
    [HttpPost("{id:guid}/scada-sync")]
    [RequirePermission(Permission.ViewDiagram)]
    public async Task<IActionResult> SyncFromScada(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channelEventService.SyncFromScadaAsync(id, cancellationToken);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _cabinetService.GetDetailListAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Create
    /// <summary> <c>ConfigureSystem</c>: kabin SCADA adresini taşır — tüm telemetri ve kumanda o adrese gider. </summary>
    [HttpPost]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Create(CabinetCreateDto request)
    {
        var result = await _cabinetService.CreateAsync(request);
        return ToAction(result);
    }
    #endregion

    #region Update
    /// <summary> Düzenleme formunun modeli (SCADA adresi dahil); yalnızca düzenleyebilene açık. </summary>
    [HttpGet("{id:guid}/update")]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(Guid id)
    {
        var result = await _cabinetService.GetUpdateModelAsync(id: id);
        return ToAction(result);
    }

    /// <summary> SCADA adresi ve bakım kararı da buradan yazılır. </summary>
    [HttpPut]
    [RequirePermission(Permission.ConfigureSystem)]
    public async Task<IActionResult> Update(CabinetUpdateDto request)
    {
        var result = await _cabinetService.UpdateAsync(request);
        return ToAction(result);
    }
    #endregion
}
