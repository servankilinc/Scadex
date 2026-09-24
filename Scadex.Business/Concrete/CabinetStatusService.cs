using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Monitoring.Commands;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Entities;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

public partial class CabinetStatusService : ICabinetStatusService
{
    private const int MaxErrorLength = 512;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IDiagramNotifier _notifier;
    private readonly ILogger<CabinetStatusService> _logger;

    public CabinetStatusService(IUnitOfWork unitOfWork, IValidationService validationService, IDiagramNotifier notifier, ILogger<CabinetStatusService> logger)
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
            await PublishDeviceChangesAsync(device.CabinetId, [ToChange(device)], cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RecordScadaContactAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync(where: c => c.Id == cabinetId, tracking: true, cancellationToken: cancellationToken);
        if (cabinet == null)
            return;

        var now = DateTime.UtcNow;
        cabinet.LastSeen = now;

        var changes = new List<DeviceStatusChange>();
        foreach (var module in await LoadControlModulesAsync(cabinetId, cancellationToken))
        {
            if (MarkReachable(module, now))
                changes.Add(ToChange(module));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes.Count > 0)
            await PublishDeviceChangesAsync(cabinetId, changes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordScadaUnreachableAsync(Guid cabinetId, string? error, CancellationToken cancellationToken = default)
    {
        var changes = new List<DeviceStatusChange>();
        foreach (var module in await LoadControlModulesAsync(cabinetId, cancellationToken))
        {
            module.LastConnectionError = error.Truncate(MaxErrorLength);
            if (MarkUnreachable(module))
                changes.Add(ToChange(module));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes.Count > 0)
        {
            _logger.LogWarning($"Kabin {cabinetId}: SCADA'ya ulasilamadi, kontrol modulu Offline'a cekildi ({error})");
            await PublishDeviceChangesAsync(cabinetId, changes, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync(where: c => c.Id == cabinetId && c.IsActive, tracking: true, cancellationToken: cancellationToken);
        if (cabinet == null)
            return;

        // Secimli okuma takipsizdir ve VERITABANINI okur: cagiran degisikliklerini bundan once kaydetmis olmali.
        var deviceStatuses = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.DeviceStatusId, IsControlModule = d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule },
            where: d => d.CabinetId == cabinetId && d.IsActive && d.DeviceStatusId != null,
            cancellationToken: cancellationToken
        ) ?? [];

        bool hasOfflineCamera = await _unitOfWork.Cameras.IsExistAsync(
            where: c => c.CabinetId == cabinetId && c.IsActive && c.IsMonitoringEnabled && c.DeviceStatusId == (int)DeviceStatus.Offline,
            cancellationToken: cancellationToken
        );

        var next = CalculateCabinetStatus(deviceStatuses.Select(d => CabinetContribution(d.DeviceStatusId, d.IsControlModule)), hasOfflineCamera);
        if (next == cabinet.DeviceStatusId)
            return;

        cabinet.DeviceStatusId = next;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishCabinetChangeAsync(cabinet, cancellationToken);
    }

    #region Kurallar
    /// <summary>
    /// Ulasildi: <c>LastSeen</c> ve hata tazelenir; <c>null</c>/<c>Offline</c> → <c>Online</c>.
    /// Warning/Critical/Maintenance canliligin karari degildir, korunur.
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

    /// <summary> Ulasilamadi: <c>null</c>/<c>Online</c> → <c>Offline</c>. Warning/Critical/Maintenance korunur. </summary>
    private static bool MarkUnreachable(Device device)
    {
        if (device.DeviceStatusId is not (null or (int)DeviceStatus.Online))
            return false;

        device.DeviceStatusId = (int)DeviceStatus.Offline;
        return true;
    }

    /// <summary>
    /// Basarisiz sonda cihazi ILK basarisizlikta <c>Offline</c>'a ceker — kamerayla ayni kural (2026-09-24 karari); hata
    /// <c>LastConnectionError</c>'a yazilir. Kontrol modulu olmayan cihazin Offline'i kabine <c>Warning</c> olarak yansir
    /// (bkz. <see cref="CabinetContribution"/>).
    /// </summary>
    private static bool MarkProbeFailed(Device device, string? error)
    {
        device.LastConnectionError = error.Truncate(MaxErrorLength);
        return MarkUnreachable(device);
    }

    /// <summary>
    /// Bir cihazin kabin durumuna katkisi. Kabinin TABANI kontrol modulunun (SCADA karti) durumudur: Online / Offline / null.
    /// Kontrol modulu OLMAYAN cihazlar kabini yalnizca KOTULESTIREBILIR (2026-09-24 karari): Offline'lari kabine <c>Warning</c>
    /// olarak katilir (kameradaki gibi), Online'lari hic katilmaz — bir POS ulasilabilir diye SCADA'si bilinmeyen kabin
    /// "Online" gorunmemeli. Cihazin kendi durumu degismez; yalnizca kabine yansimasi.
    /// </summary>
    private static int? CabinetContribution(int? status, bool isControlModule)
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
    /// Kabin durumu = cihaz katkilarinin (<see cref="CabinetContribution"/>) en kotusu; izlenen bir kamera Offline ise
    /// <c>Warning</c> katkisi. <see cref="RecalculateAsync"/> ve tarama uzlastirmasi AYNI kurali kullanir.
    /// </summary>
    private static int? CalculateCabinetStatus(IEnumerable<int?> deviceContributions, bool hasOfflineCamera)
    {
        int? worst = null;
        int worstRank = -1;

        // Online kamera katki VERMEZ: SCADA'dan hic ses gelmemis kabin yalnizca kamerayla "Online" gorunmemeli.
        var candidates = hasOfflineCamera ? deviceContributions.Append((int)DeviceStatus.Warning) : deviceContributions;

        foreach (var candidate in candidates)
        {
            if (candidate is not int status)
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
    /// <summary> Kabinin aktif kontrol modulleri (SCADA karti) — pasif SCADA kanitinin sahibi. Takipli. </summary>
    private async Task<ICollection<Device>> LoadControlModulesAsync(Guid cabinetId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Devices.GetAllAsync(
            where: d => d.CabinetId == cabinetId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule,
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];
    }

    /// <summary> Cihaz degisikliklerini yayinlar ve kabini yeniden hesaplar. Degisiklikler KAYDEDILMIS olmali. </summary>
    private async Task PublishDeviceChangesAsync(Guid cabinetId, IReadOnlyList<DeviceStatusChange> changes, CancellationToken cancellationToken)
    {
        await _notifier.DeviceStatusesChangedAsync(cabinetId, changes, cancellationToken);
        await RecalculateAsync(cabinetId, cancellationToken);
    }

    /// <summary> Kabin durumunu hem kabini izleyenlere (diyagram) hem de tum kabinleri izleyenlere (harita, liste) yayinlar. </summary>
    private async Task PublishCabinetChangeAsync(Cabinet cabinet, CancellationToken cancellationToken)
    {
        var change = new CabinetStatusChange
        {
            CabinetId = cabinet.Id,
            StatusId = (DeviceStatus?)cabinet.DeviceStatusId,
            LastSeen = cabinet.LastSeen,
            ScadaLastIngestAt = cabinet.ScadaLastIngestAt
        };

        await _notifier.CabinetStatusChangedAsync(change, cancellationToken);
        await _notifier.CabinetStatusChangedForAllAsync(change, cancellationToken);
    }

    private static DeviceStatusChange ToChange(Device device) => new()
    {
        DeviceId = device.Id,
        StatusId = (DeviceStatus?)device.DeviceStatusId,
        LastSeen = device.LastSeen
    };
    #endregion
}
