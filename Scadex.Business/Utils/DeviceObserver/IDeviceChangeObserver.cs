namespace Scadex.Business.Utils.DeviceObserver;

/// <summary>
/// Cihaz bilgisinin değiştiğini (ya da cihazın pasife alındığını) çekirdek dışındaki modüllere bildiren kanca. Çekirdek modülü bilmez:
/// modül (örn. CameraManagement — kamera bilgisi değişince MediaMTX yolunu düşürür) kendi uygulamasını DI'a kaydeder.
/// <para/>
/// Yazım <b>commit edildikten SONRA</b> çağrılır (<c>DiagramController.UpdateDevice</c> / <c>Delete</c>, <c>CameraService.UpdateAsync</c>):
/// geri alınabilecek bir yazımdan önce dış dünyaya dokunulmasın diye. Tipe bakılmaz; ilgilenmediği Id'leri gözlemci kendisi ayıklar.
/// Başarısızlık yalnızca loglanır, yazımı geri almaz.
/// </summary>
public interface IDeviceChangeObserver
{
    /// <summary> Verilen cihazların bilgisi değişti ya da cihazlar pasife alındı (diyagramdan kaldırıldı). </summary>
    Task OnDevicesChangedAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken = default);
}
