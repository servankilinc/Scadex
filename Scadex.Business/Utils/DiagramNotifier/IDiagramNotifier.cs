using Scadex.Model.Realtime.Notifications;

namespace Scadex.Business.Utils.DiagramNotifier;

public interface IDiagramNotifier
{
    Task ChannelValuesChangedAsync(Guid automationId, IReadOnlyList<ChannelValueChangeNotify> changes, CancellationToken cancellationToken = default);

    Task DeviceStatusesChangedAsync(Guid automationId, IReadOnlyList<DeviceStatusChangeNotify> changes, CancellationToken cancellationToken = default);

    Task AutomationStatusChangedAsync(AutomationStatusChangeNotify change, CancellationToken cancellationToken = default);

    /// <summary> Otomasyon durumunu tüm otomasyonları izleyenlere (ana sayfa haritası, otomasyon listesi vs.) bildirir. Yalnizca status değişince bildirir </summary>
    Task AutomationStatusChangedForAllAsync(AutomationStatusChangeNotify change, CancellationToken cancellationToken = default);

    /// <summary>Bir komutun sonuclandigini ayni otomasyonu izleyen DIGER kullanicilara bildirir. Komutu gonderen sonucu zaten HTTP yanitinda alir. </summary>
    Task CommandCompletedAsync(Guid automationId, CommandCompletedNotify change, CancellationToken cancellationToken = default);

    /// <summary> Otomasyonun kart okuyucusunda kart okutuldu; otomasyonu izleyenlere  bildirir. </summary>
    Task CardReadAsync(Guid automationId, CardReadNotify change, CancellationToken cancellationToken = default);
}
