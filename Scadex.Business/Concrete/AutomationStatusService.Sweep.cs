using Scadex.Business.Abstract;
using Scadex.Model.Entities;
using Scadex.Model.Realtime.Notifications;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

public partial class AutomationStatusService
{
    /// <summary>
    /// Monitoring özelliği kapalı bir entity (kontrol modülü dahil) Online/Offline durumunda bu süreden uzun kaldıysa artık "bilinmiyor"dur.
    /// Monitoring özelliği açık olanlar kapsam dışı: onları worker service ile kontrol edilip offline/online durumlaına geçebiliyor. <para/>
    /// <c>LastSeen</c>'i BOŞ olan kayıt "eski" sayılmaz: hiç görülmemiş bir kontrol kartının <c>NoResponse</c> ile gelen Offline'ı
    /// (NoResponse <c>LastSeen</c> yazmaz) bir sonraki taramada silinmesin. Bedeli: hiç görülmemiş ama Online/Offline kalmış kayıtlar
    /// taramayla temizlenmez (bilinçli).
    /// </summary>
    private static readonly TimeSpan StatusExpireAfter = TimeSpan.FromHours(23);

    /// <inheritdoc />
    public async Task<AutomationStatusSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow - StatusExpireAfter;
        var changesByAutomation = new Dictionary<Guid, List<DeviceStatusChangeNotify>>();

        // 1) Cihaz temizligi: izlemesi KAPALI (kontrol modulu dahil) ve son haberleşme 23 saatten eski cihazin Online/Offline'i artik bilinmiyor olanlar. 
        var expiredDevices = await _unitOfWork.Devices.GetAllAsync(
            where: d =>
                d.IsActive &&
                !(d.IsMonitoringEnabled && d.ComponentTemplate!.IsMonitorable) &&
                d.LastSeen != null && d.LastSeen < threshold &&
                (d.DeviceStatusId == (int)DeviceStatus.Online || d.DeviceStatusId == (int)DeviceStatus.Offline),
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];

        foreach (var device in expiredDevices)
        {
            device.DeviceStatusId = null;
            device.LastConnectionError = null;
            AddChange(changesByAutomation, device);
        }
        

        if (expiredDevices.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var (automationId, changes) in changesByAutomation)
                await _notifier.DeviceStatusesChangedAsync(automationId, changes, cancellationToken);
        }

        // 2) Uzlastirma: cihaz durumunu degistirmeden otomasyonu etkileyen yavas yollar (diyagramda cihaz silme, izlemeyi
        //    kapatma) burada kapanir. 1. adimin otomasyonları da bununla hesaplanir.
        int reconciled = await ReconcileAllAutomationsAsync(cancellationToken);

        return new AutomationStatusSweepResult(expiredDevices.Count, reconciled);
    }

    /// <summary>
    /// Tum aktif otomasyonların durumunu tek seferde (otomasyon basina degil, gruplu okumayla) yeniden hesaplar; yalnizca
    /// saklı degeri farkli olanlari takipli yukleyip yazar ve yayinlar. Kural <see cref="RecalculateAsync"/> ile aynidir.
    /// </summary>
    /// <returns> Durumu duzeltilen otomasyon sayisi. </returns>
    private async Task<int> ReconcileAllAutomationsAsync(CancellationToken cancellationToken)
    {
        var automations = await _unitOfWork.Automations.GetAllAsync(
            select: c => new { c.Id, c.DeviceStatusId },
            // Bakimdaki otomasyon, uzlastirmaya girmez
            where: c => c.IsActive && c.DeviceStatusId != (int)DeviceStatus.Maintenance,
            cancellationToken: cancellationToken
        ) ?? [];
        if (automations.Count == 0)
            return 0;

        var deviceStatuses = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.AutomationId, d.DeviceStatusId, IsControlModule = d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule },
            where: d => d.IsActive && d.DeviceStatusId != null,
            cancellationToken: cancellationToken
        ) ?? [];

        var contributionsByAutomation = deviceStatuses.ToLookup(d => d.AutomationId, d => AutomationContribution(d.DeviceStatusId, d.IsControlModule));

        var drifted = new Dictionary<Guid, int?>();
        foreach (var automation in automations)
        {
            var next = CalculateAutomationStatus(contributionsByAutomation[automation.Id]);
            if (next != automation.DeviceStatusId)
                drifted[automation.Id] = next;
        }
        if (drifted.Count == 0)
            return 0;

        var driftedIds = drifted.Keys.ToList();
        var tracked = await _unitOfWork.Automations.GetAllAsync(
            where: c => driftedIds.Contains(c.Id),
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];

        foreach (var automation in tracked)
            automation.DeviceStatusId = drifted[automation.Id];

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var automation in tracked)
            await PublishAutomationChangeAsync(automation, cancellationToken);

        return tracked.Count;
    }

    private static void AddChange(Dictionary<Guid, List<DeviceStatusChangeNotify>> changesByAutomation, Device device)
    {
        if (!changesByAutomation.TryGetValue(device.AutomationId, out var list))
            changesByAutomation[device.AutomationId] = list = [];

        list.Add(ToChange(device));
    }
}
