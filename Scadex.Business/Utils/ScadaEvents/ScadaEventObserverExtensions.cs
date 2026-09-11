using Microsoft.Extensions.Logging;

namespace Scadex.Business.Utils.ScadaEvents;

public static class ScadaEventObserverExtensions
{
    /// <summary> Kayıtlı her dinleyici sırayla çağırılır </summary>
    public static async Task PublishAsync(this IEnumerable<IScadaEventObserver> observers, Func<IScadaEventObserver, Task> publish, ILogger logger, string eventName)
    {
        foreach (var observer in observers)
        {
            try
            {
                await publish(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "SCADA(EventObserver) dinleyicisi {Observer} '{Event}' olayinda hata verdi; istek etkilenmedi.", observer.GetType().Name, eventName);
            }
        }
    }
}
