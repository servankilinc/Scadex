using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Monitoring.Commands;

namespace Scadex.Business.Abstract;

/// <summary>
/// Kabin durumunun ve cihazların çevirimiçi durumunun kontrol eden servisimiz. <para/>
/// Device'lar offline durumunda ise Kabin <c>Warning</c> olur. kontrol kartı (SCADA kartı) <c>Offline</c> ise kabin <c>Offline</c> olur. <para/>
/// Kabini <c>Online</c>'a yalnızca kontrol kartı çeker; diğer cihaz ve kameralar kabini yalnızca kötüleştirebilir (Warning katar). <para/>
/// </summary>
public interface ICabinetStatusService
{
    /// <summary> Bir <c>Device</c> ping sonucunu yazar; kamerayla aynı kural: ilk başarısızlıkta <c>Offline</c>'a çeker </summary>
    Task<Result> RecordDeviceProbeResultAsync(Guid deviceId, MonitoredAssetProbeResultDto result, CancellationToken cancellationToken = default);

    /// <summary> SCADA'dan gelen bir bilgi varsa kabinin scada kontrol kartı <c>Online</c>'a çekilir, <c>LastSeen</c> ve <c>Cabinet.LastSeen</c> tazelenir </summary>
    Task RecordScadaContactAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary> SCADA'ya ulaşılamadığında kabinin scada kontrol kartı <c>Offline</c>'a çekilir  </summary>
    Task RecordScadaUnreachableAsync(Guid cabinetId, string? error, CancellationToken cancellationToken = default);

    /// <summary> Kabin durumunu yeniden hesaplar; değiştiyse yazar ve yayınlar (örn. kamera durumu değişince). </summary>
    Task RecalculateAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 1) izlemesi kapalı cihazların durumunu → <c>null</c> (bilinmiyor) yapar.
    /// 2) Tüm aktif kabinlerin uzlaştırılması (diyagramda cihaz silme, kamera pasife alma gibi yavaş yollar).
    /// </summary>
    Task<CabinetStatusSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

/// <summary> Tarama turunun özeti — yalnızca log içindir. </summary>
public readonly record struct CabinetStatusSweepResult(int ClearedDevices, int ClearedCameras, int ReconciledCabinets);
