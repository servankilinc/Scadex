using Scadex.Model.Dtos.Scada.Events;

namespace Scadex.Business.Utils.ScadaEvents;

/// <summary>
/// SCADA'dan gelen "İnput değişikliği", "kart okutucuya okutulan kart bilgisi" gibi eventleri, Scadex çekirdeği(Core,Model,DataAccess,Business ve Api) 
/// dışındaki modüllere (örneğin Scadex.Signalization) ileten bir hook yapısı. 
/// <para /> 
/// Scadex çekirdeği proje ve müşteri bazında özelleştirilemez modüllere  ayrılarak alt  proje modüllerine ayrılabilir 
/// örneğin Sinyalizasyon kabinleri ve iş süreçleri için "Scadex.Signalization" projesinin oluşturulması gibi 
/// (NOT: firma/kurum bazlı değil yarın başka bir kurum da Sinyalizasyon projesi istediğinde aynı modülü kullanabilmeli)
/// </summary>
public interface IScadaEventObserver
{
    /// <summary> Bir kanalın değeri değişti (aynı değer için tekrar çağırılmaz). </summary>
    Task OnChannelChangedAsync(ChannelChangedNotification notification, CancellationToken cancellationToken = default);

    /// <summary> Kabinde ki bir kart okuyucuya kart okutuldu (kart ve kullanıcı tanımsız olsa da çağırılır zaten nullable yaptık). </summary>
    Task OnCardPresentedAsync(CardPresentedNotification notification, CancellationToken cancellationToken = default);
}
