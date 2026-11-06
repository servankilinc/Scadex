using Microsoft.Extensions.Logging;

namespace Scadex.Business.Utils.DeviceObserver;

public static class DeviceChangeObserverExtensions
{
    /// <summary> Kayıtlı her dinleyici sırayla çağrılır; boş listede hiçbiri çağrılmaz. </summary>
    public static async Task PublishDevicesChangedAsync(this IEnumerable<IDeviceChangeObserver> observers, IReadOnlyCollection<Guid> deviceIds, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0) return;

        foreach (var observer in observers)
        {
            try
            {
                await observer.OnDevicesChangedAsync(deviceIds, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Cihaz degisikligi dinleyicisi {Observer} hata verdi; yazim etkilenmedi.", observer.GetType().Name);
            }
        }
    }
}
