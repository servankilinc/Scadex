using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Entities;
using Scadex.Model.Realtime.Notifications;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

public partial class AutomationStatusService : IAutomationStatusService
{
    private const int MaxErrorLength = 512;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IDiagramNotifier _notifier;
    private readonly ILogger<AutomationStatusService> _logger;

    public AutomationStatusService(IUnitOfWork unitOfWork, IValidationService validationService, IDiagramNotifier notifier, ILogger<AutomationStatusService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> RecordDeviceProbeResultAsync(Guid deviceId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(result, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for MonitoredAssetProbeResultDto");

        var device = await _unitOfWork.Devices.GetAsync(
            where: d => d.Id == deviceId && d.IsActive,
            tracking: true,
            cancellationToken: cancellationToken
        );
        if (device == null)
            return Result.NotFound(description: "Cihaz bulunamadi veya pasif durumda");

        bool statusChanged = result.Reachable
            ? MarkReachable(device, DateTime.UtcNow)
            : MarkProbeFailed(device, result.Error);

        // Degisiklik yoksa (ayni hata, ayni durum) EF veritabanina gitmez.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (statusChanged)
            await PublishDeviceChangesAsync(device.AutomationId, [ToChange(device)], cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RecordScadaContactAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var automation = await _unitOfWork.Automations.GetAsync(where: c => c.Id == automationId, tracking: true, cancellationToken: cancellationToken);
        if (automation == null)
            return;

        var now = DateTime.UtcNow;
        automation.LastSeen = now;

        var changes = new List<DeviceStatusChangeNotify>();
        foreach (var module in await LoadControlModulesAsync(automationId, cancellationToken))
        {
            if (MarkReachable(module, now))
                changes.Add(ToChange(module));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes.Count > 0)
            await PublishDeviceChangesAsync(automationId, changes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordScadaUnreachableAsync(Guid automationId, string? error, CancellationToken cancellationToken = default)
    {
        var changes = new List<DeviceStatusChangeNotify>();
        foreach (var module in await LoadControlModulesAsync(automationId, cancellationToken))
        {
            module.LastConnectionError = error.Truncate(MaxErrorLength);
            if (MarkUnreachable(module))
                changes.Add(ToChange(module));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes.Count > 0)
        {
            _logger.LogWarning($"Otomasyon {automationId}: SCADA'ya ulasilamadi, kontrol modulu Offline'a cekildi ({error})");
            await PublishDeviceChangesAsync(automationId, changes, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var automation = await _unitOfWork.Automations.GetAsync(where: c => c.Id == automationId && c.IsActive, tracking: true, cancellationToken: cancellationToken);
        // Bakim yapiskandir: yalnizca SetMaintenanceAsync kaldirir.
        if (automation == null || automation.DeviceStatusId == (int)DeviceStatus.Maintenance)
            return;

        await ApplyCalculatedStatusAsync(automation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetMaintenanceAsync(Guid automationId, bool underMaintenance, CancellationToken cancellationToken = default)
    {
        var automation = await _unitOfWork.Automations.GetAsync(where: c => c.Id == automationId && c.IsActive, tracking: true, cancellationToken: cancellationToken);
        if (automation == null)
            return;

        bool isUnderMaintenance = automation.DeviceStatusId == (int)DeviceStatus.Maintenance;
        if (isUnderMaintenance == underMaintenance)
            return;

        if (!underMaintenance)
        {
            // Bakimdan cikis: durum canliliktan yeniden hesaplanir (hesaplama Maintenance uretmez, deger mutlaka degisir).
            await ApplyCalculatedStatusAsync(automation, cancellationToken);
            return;
        }

        automation.DeviceStatusId = (int)DeviceStatus.Maintenance;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishAutomationChangeAsync(automation, cancellationToken);
    }

    /// <summary> Otomasyon durumunu cihaz katkilarindan hesaplar; farkliysa yazar ve yayinlar. <paramref name="automation"/> takipli olmali </summary>
    private async Task ApplyCalculatedStatusAsync(Automation automation, CancellationToken cancellationToken)
    {
        Guid automationId = automation.Id;

        // Secimli okuma takipsizdir ve VERITABANINI okur: cagiran degisikliklerini bundan once kaydetmis olmali.
        var deviceStatuses = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.DeviceStatusId, IsControlModule = d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule },
            where: d => d.AutomationId == automationId && d.IsActive && d.DeviceStatusId != null,
            cancellationToken: cancellationToken
        ) ?? [];

        var next = CalculateAutomationStatus(deviceStatuses.Select(d => AutomationContribution(d.DeviceStatusId, d.IsControlModule)));
        if (next == automation.DeviceStatusId)
            return;

        automation.DeviceStatusId = next;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishAutomationChangeAsync(automation, cancellationToken);
    }

    #region Kurallar
    /// <summary>
    /// Ulasildi: <c>LastSeen</c> ve hata tazelenir; <c>null</c>/<c>Offline</c> → <c>Online</c>.
    /// Warning/Maintenance canliligin karari degildir, korunur.
    /// </summary>
    /// <returns> Durum degisti mi. </returns>
    private static bool MarkReachable(Device device, DateTime now)
    {
        device.LastSeen = now;
        device.LastConnectionError = null;

        if (device.DeviceStatusId is not (null or (int)DeviceStatus.Offline))
            return false;

        device.DeviceStatusId = (int)DeviceStatus.Online;
        return true;
    }

    /// <summary> Ulasilamadi: <c>null</c>/<c>Online</c> → <c>Offline</c>. Warning/Maintenance korunur. </summary>
    private static bool MarkUnreachable(Device device)
    {
        if (device.DeviceStatusId is not (null or (int)DeviceStatus.Online))
            return false;

        device.DeviceStatusId = (int)DeviceStatus.Offline;
        return true;
    }

    /// <summary> Basarisiz sonda cihazi ILK basarisizlikta <c>Offline</c>'a ceker hata <c>LastConnectionError</c>'a yazilir. Kontrol modulu olmayan cihazin Offline'i otomasyona <c>Warning</c> olarak yansır </summary>
    private static bool MarkProbeFailed(Device device, string? error)
    {
        device.LastConnectionError = error.Truncate(MaxErrorLength);
        return MarkUnreachable(device);
    }

    /// <summary>
    /// Bir cihazin otomasyon durumuna katkisi. Otomasyonun TABANI kontrol modulunun (SCADA karti) durumudur: Online / Offline / null.
    /// Kontrol modulu OLMAYAN cihazlar otomasyonu yalnizca KOTULESTIREBILIR: Offline'lari otomasyona <c>Warning</c> olarak katilir, Online'lari hic katilmaz — bir POS ya da kamera ulasilabilir diye SCADA'si bilinmeyen otomasyon
    /// "Online" gorunmemeli. Cihazin kendi durumu degismez; yalnizca otomasyona yansimasi.
    /// </summary>
    private static int? AutomationContribution(int? status, bool isControlModule)
    {
        if (isControlModule)
            return status;

        return status switch
        {
            (int)DeviceStatus.Offline => (int)DeviceStatus.Warning,
            (int)DeviceStatus.Online => null,
            _ => status
        };
    }

    /// <summary>
    /// Otomasyon durumu = cihaz katkilarinin (<see cref="AutomationContribution"/>) en kotusu.
    /// <see cref="RecalculateAsync"/> ve tarama uzlastirmasi AYNI kurali kullanir.
    /// Hesaplama HICBIR ZAMAN <c>Maintenance</c> uretmez: otomasyonda <c>Maintenance</c> yalnizca operatorun elle verdigi
    /// yapiskan karardir (<see cref="SetMaintenanceAsync"/>). Bir cihazin Maintenance'i otomasyona taşınsaydı
    /// otomasyon kendiliginden bakima duser ve hesaplama ona bir daha dokunmazdi.
    /// </summary>
    private static int? CalculateAutomationStatus(IEnumerable<int?> deviceContributions)
    {
        int? worst = null;
        int worstRank = -1;

        foreach (var candidate in deviceContributions)
        {
            if (candidate is not int status || status == (int)DeviceStatus.Maintenance)
                continue;
            int rank = Model.Enums.EntityEnums.DeviceStatusSeverityRank(status);
            if (rank <= worstRank)
                continue;
            worstRank = rank;
            worst = status;
        }

        return worst;
    }
    #endregion

    #region Helpers
    /// <summary> Otomasyonun aktif kontrol modulleri (SCADA karti) — pasif SCADA kanitinin sahibi. Takipli. </summary>
    private async Task<ICollection<Device>> LoadControlModulesAsync(Guid automationId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Devices.GetAllAsync(
            where: d => d.AutomationId == automationId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule,
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];
    }

    /// <summary> Cihaz degisikliklerini yayinlar ve otomasyonu yeniden hesaplar. Degisiklikler KAYDEDILMIS olmali. </summary>
    private async Task PublishDeviceChangesAsync(Guid automationId, IReadOnlyList<DeviceStatusChangeNotify> changes, CancellationToken cancellationToken)
    {
        await _notifier.DeviceStatusesChangedAsync(automationId, changes, cancellationToken);
        await RecalculateAsync(automationId, cancellationToken);
    }

    /// <summary> Otomasyon durumunu hem otomasyonu izleyenlere (diyagram) hem de tum otomasyonları izleyenlere (harita, liste) yayinlar. </summary>
    private async Task PublishAutomationChangeAsync(Automation automation, CancellationToken cancellationToken)
    {
        // Son ingest kartın alanıdır (2026-11-04); durum değişimi seyrek olduğu için ek sorgu sorun değil.
        var card = await _unitOfWork.ControlCards.GetActiveForAutomationAsync(automation.Id, cancellationToken: cancellationToken);
        var change = new AutomationStatusChangeNotify
        {
            AutomationId = automation.Id,
            StatusId = (DeviceStatus?)automation.DeviceStatusId,
            LastSeen = automation.LastSeen,
            ScadaLastIngestAt = card?.ScadaLastIngestAt
        };

        await _notifier.AutomationStatusChangedAsync(change, cancellationToken);
        await _notifier.AutomationStatusChangedForAllAsync(change, cancellationToken);
    }

    private static DeviceStatusChangeNotify ToChange(Device device) => new()
    {
        DeviceId = device.Id,
        StatusId = (DeviceStatus?)device.DeviceStatusId,
        LastSeen = device.LastSeen
    };
    #endregion
}
