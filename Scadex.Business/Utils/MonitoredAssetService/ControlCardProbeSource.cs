using Microsoft.Extensions.Configuration;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Monitoring.Queries;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.MonitoredAssetService;

/// <summary>
/// İzlemesi açık kontrol kartları (SCADA). TCP/ICMP yerine kartın okuma ucu çağrılır (<c>GET {ScadaBaseUrl}/status</c>,
/// <see cref="IChannelEventService.SyncFromScadaAsync"/>): her tur hem canlılık kanıtı hem kanal senkronudur — kaçırılan ingest,
/// resetlenen röle ya da elle müdahale <c>PingIntervalSec</c> içinde düzelir. Hedef adres cihazın IP'si değil kartın SCADA adresidir (<c>ControlCard.ScadaBaseUrl</c>).
/// </summary>
public class ControlCardProbeSource : IMonitoredAssetProbeSource
{
    private const int DefaultProbeTimeoutMs = 3000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutomationStatusService _automationStatusService;
    private readonly IChannelEventService _channelEventService;

    /// <summary>
    /// TCP sondasıyla aynı ayar (<c>Monitoring:ProbeTimeoutMs</c>), otomasyonun komut zaman aşımı DEĞİL: worker kaynakları sırayla yoklar,
    /// yanıtsız kartlar turu dakikalarca bekletmesin.
    /// </summary>
    private readonly TimeSpan _probeTimeout;

    /// <summary> Bu turun hedeflerinin otomasyonu (cihaz Id -> otomasyon Id). Kaynak her turda yeni scope'ta kurulur, tur boyunca yaşar. </summary>
    private readonly Dictionary<Guid, Guid> _automationByDevice = [];

    public ControlCardProbeSource(IUnitOfWork unitOfWork, IAutomationStatusService automationStatusService, IChannelEventService channelEventService, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _automationStatusService = automationStatusService;
        _channelEventService = channelEventService;
        _probeTimeout = TimeSpan.FromMilliseconds(Math.Max(250, configuration.GetValue("Monitoring:ProbeTimeoutMs", DefaultProbeTimeoutMs)));
    }

    /// <inheritdoc/>
    public string AssetTypeName => "ControlCard";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonitoredAssetProbeTargetDto>> GetProbeTargetsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new
            {
                d.Id,
                d.Name,
                d.AutomationId,
                d.ControlCard!.ScadaBaseUrl,
                d.PingIntervalSec
            },
            // SCADA'si kapali ya da adresi olmayan kart okunamaz: hic yoklanmaz (DeviceProbeSource da almaz).
            where: d => d.IsActive && d.IsMonitoringEnabled && d.ComponentTemplate!.IsMonitorable
                && d.ComponentTemplate.DeviceTypeId == (int)DeviceType.ControlModule
                && d.Automation!.IsActive && d.ControlCard!.ScadaIsEnabled && d.ControlCard.ScadaBaseUrl != null && d.ControlCard.ScadaBaseUrl != "",
            cancellationToken: cancellationToken
        ) ?? [];

        _automationByDevice.Clear();
        foreach (var row in rows)
            _automationByDevice[row.Id] = row.AutomationId;

        return [.. rows.Select(r => new MonitoredAssetProbeTargetDto
        {
            Id = r.Id,
            Name = r.Name,
            IpAddress = r.ScadaBaseUrl!, // yalnızca günlük için; sonda adresi karttan okur (SyncFromScadaAsync)
            MonitoringPort = null,
            PingIntervalSec = r.PingIntervalSec
        })];
    }

    /// <inheritdoc/>
    public async Task<MonitoredAssetProbeResultDto?> TryProbeAsync(MonitoredAssetProbeTargetDto target, CancellationToken cancellationToken = default)
    {
        if (!_automationByDevice.TryGetValue(target.Id, out var automationId))
            return null;

        var sync = await _channelEventService.SyncFromScadaAsync(automationId, _probeTimeout, cancellationToken);

        // Yapılandırma tur arasında değişti (SCADA kapatıldı, adres silindi): kartı Offline'a çekmek yanlış olur, varsayılan sondaya bırak.
        if (!sync.IsSuccess || sync.Data == null)
            return null;

        // Kart cevap verdiyse (hata yanıtı dahil, ör. /status'u bilmeyen eski firmware 404) ayaktadır — Offline'a çekilmez, yoksa ingest'le
        // Online / turla Offline arasında gidip gelirdi. Yalnızca yanıt yoksa ulaşılamaz.
        return sync.Data.Status == CommandStatus.NoResponse
            ? new MonitoredAssetProbeResultDto { Reachable = false, Error = sync.Data.Message ?? "SCADA yanıt vermedi" }
            : new MonitoredAssetProbeResultDto { Reachable = true };
    }

    /// <inheritdoc/>
    public Task<Result> RecordProbeResultAsync(Guid assetId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default) =>
        _automationStatusService.RecordDeviceProbeResultAsync(assetId, result, cancellationToken);
}
