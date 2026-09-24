using Scadex.Business.Abstract;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Entities;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

public partial class CabinetStatusService
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
    public async Task<CabinetStatusSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow - StatusExpireAfter;
        var changesByCabinet = new Dictionary<Guid, List<DeviceStatusChange>>();

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
            AddChange(changesByCabinet, device);
        }

        // 2) Kamera temizligi: ayni kural, izlemesi kapali kameralar icin. Kamerada SignalR durum olayi yok; ekran REST'ten okur.
        var expiredCameras = await _unitOfWork.Cameras.GetAllAsync(
            where: c =>
                c.IsActive &&
                !c.IsMonitoringEnabled &&
                c.LastSeen != null && c.LastSeen < threshold &&
                (c.DeviceStatusId == (int)DeviceStatus.Online || c.DeviceStatusId == (int)DeviceStatus.Offline),
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];

        foreach (var camera in expiredCameras)
        {
            camera.DeviceStatusId = null;
            camera.LastConnectionError = null;
        }

        if (expiredDevices.Count > 0 || expiredCameras.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var (cabinetId, changes) in changesByCabinet)
                await _notifier.DeviceStatusesChangedAsync(cabinetId, changes, cancellationToken);
        }

        // 3) Uzlastirma: cihaz/kamera durumunu degistirmeden kabini etkileyen yavas yollar (diyagramda cihaz silme,
        //    kamera pasife alma / izlemeyi kapatma) burada kapanir. 1-2. adimlarin kabinleri de bununla hesaplanir.
        int reconciled = await ReconcileAllCabinetsAsync(cancellationToken);

        return new CabinetStatusSweepResult(expiredDevices.Count, expiredCameras.Count, reconciled);
    }

    /// <summary>
    /// Tum aktif kabinlerin durumunu tek seferde (kabin basina degil, gruplu okumayla) yeniden hesaplar; yalnizca
    /// saklı degeri farkli olanlari takipli yukleyip yazar ve yayinlar. Kural <see cref="RecalculateAsync"/> ile aynidir.
    /// </summary>
    /// <returns> Durumu duzeltilen kabin sayisi. </returns>
    private async Task<int> ReconcileAllCabinetsAsync(CancellationToken cancellationToken)
    {
        var cabinets = await _unitOfWork.Cabinets.GetAllAsync(
            select: c => new { c.Id, c.DeviceStatusId },
            where: c => c.IsActive,
            cancellationToken: cancellationToken
        ) ?? [];
        if (cabinets.Count == 0)
            return 0;

        var deviceStatuses = await _unitOfWork.Devices.GetAllAsync(
            select: d => new { d.CabinetId, d.DeviceStatusId, IsControlModule = d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule },
            where: d => d.IsActive && d.DeviceStatusId != null,
            cancellationToken: cancellationToken
        ) ?? [];

        var offlineCameraCabinetIds = (await _unitOfWork.Cameras.GetAllAsync(
            select: c => c.CabinetId,
            where: c => c.IsActive && c.IsMonitoringEnabled && c.DeviceStatusId == (int)DeviceStatus.Offline,
            cancellationToken: cancellationToken
        ) ?? []).ToHashSet();

        var contributionsByCabinet = deviceStatuses.ToLookup(d => d.CabinetId, d => CabinetContribution(d.DeviceStatusId, d.IsControlModule));

        var drifted = new Dictionary<Guid, int?>();
        foreach (var cabinet in cabinets)
        {
            var next = CalculateCabinetStatus(contributionsByCabinet[cabinet.Id], offlineCameraCabinetIds.Contains(cabinet.Id));
            if (next != cabinet.DeviceStatusId)
                drifted[cabinet.Id] = next;
        }
        if (drifted.Count == 0)
            return 0;

        var driftedIds = drifted.Keys.ToList();
        var tracked = await _unitOfWork.Cabinets.GetAllAsync(
            where: c => driftedIds.Contains(c.Id),
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];

        foreach (var cabinet in tracked)
            cabinet.DeviceStatusId = drifted[cabinet.Id];

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var cabinet in tracked)
            await PublishCabinetChangeAsync(cabinet, cancellationToken);

        return tracked.Count;
    }

    private static void AddChange(Dictionary<Guid, List<DeviceStatusChange>> changesByCabinet, Device device)
    {
        if (!changesByCabinet.TryGetValue(device.CabinetId, out var list))
            changesByCabinet[device.CabinetId] = list = [];

        list.Add(ToChange(device));
    }
}
