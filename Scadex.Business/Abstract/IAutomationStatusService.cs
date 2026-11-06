using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Automation.Queries;
using Scadex.Model.Dtos.Monitoring.Commands;

namespace Scadex.Business.Abstract;

/// <summary>
/// Otomasyon durumunun ve cihazların çevirimiçi durumunun kontrol eden servisimiz. <para/>
/// Device'lar offline durumunda ise Otomasyon <c>Warning</c> olur. kontrol kartı (SCADA kartı) <c>Offline</c> ise otomasyon <c>Offline</c> olur. <para/>
/// Otomasyonu <c>Online</c>'a yalnızca kontrol kartı çeker; diğer cihaz ve kameralar otomasyonu yalnızca kötüleştirebilir (Warning katar). <para/>
/// </summary>
public interface IAutomationStatusService
{
    /// <summary> Bir <c>Device</c> ping sonucunu yazar; kamerayla aynı kural: ilk başarısızlıkta <c>Offline</c>'a çeker </summary>
    Task<Result> RecordDeviceProbeResultAsync(Guid deviceId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default);

    /// <summary> SCADA'dan gelen bir bilgi varsa otomasyonun scada kontrol kartı <c>Online</c>'a çekilir, <c>LastSeen</c> ve <c>Automation.LastSeen</c> tazelenir </summary>
    Task RecordScadaContactAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary> SCADA'ya ulaşılamadığında otomasyonun scada kontrol kartı <c>Offline</c>'a çekilir  </summary>
    Task RecordScadaUnreachableAsync(Guid automationId, string? error, CancellationToken cancellationToken = default);

    /// <summary> Otomasyon durumunu yeniden hesaplar; değiştiyse yazar ve yayınlar (örn. kamera durumu değişince). Bakımdaki otomasyona dokunmaz. </summary>
    Task RecalculateAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Otomasyon durumunun dökümü (salt okunur): hesaplamanın gördüğü her cihaz/kamera ve otomasyona yansıyan katkısı — durumu hesaplayan
    /// kuralın AYNISIYLA. Otomasyon yoksa ya da pasifse 404.
    /// </summary>
    Task<Result<AutomationStatusBreakdownDto>> GetBreakdownAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary> Otomasyonu bakıma alır (<c>DeviceStatusId = Maintenance</c>) ya da bakımdan çıkarır. </summary>
    Task SetMaintenanceAsync(Guid automationId, bool underMaintenance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 1) izlemesi kapalı cihazların durumunu → <c>null</c> (bilinmiyor) yapar.
    /// 2) Tüm aktif otomasyonların uzlaştırılması (diyagramda cihaz silme, izlemeyi kapatma gibi yavaş yollar).
    /// </summary>
    Task<AutomationStatusSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

/// <summary> Tarama turunun özeti — yalnızca log içindir. </summary>
public readonly record struct AutomationStatusSweepResult(int ClearedDevices, int ReconciledAutomations);
