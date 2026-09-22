using Scadex.Signalization.Realtime;

namespace Scadex.Signalization.Hubs;

/// <summary> Sinyalizasyon hub'inin Client arayuzu: Client'lara hangi mesajlarin gonderilebilecegini tanimlar. </summary>
public interface ISignalizationHubClientContract
{
    /// <summary> Bir operator islemi eklendi ya da guncellendi (acildi, kapandi, bayrak/siren/zamanlayici degisti). </summary>
    Task OperatorSessionChanged(OperatorSessionChangedMessage message);

    /// <summary> 
    /// Kabinin bir output cihazı (siren / dış kapı aydınlatma / iç kapı kilidi) değişti. 
    /// NOT: Scadex çekirdeğinde("/hubs/diagram") output değişimleri yayınlanıyor ancak burada işlenmiş modül için spesifik bir yaynılama yapıyoruz 
    /// </summary>
    Task SignalCabinetStateChanged(SignalCabinetStateChangedMessage message);

    /// <summary>
    /// Bir kapının anahtarı (input) değişti. Sanal kabin girişleri de bu hub'dan alır; 
    /// NOT: Scadex çekirdeğinde("/hubs/diagram") input değişimleri yayınlanıyor ancak burada işlenmiş modül için spesifik bir yaynılama yapıyoruz
    /// </summary>
    Task SignalDoorSwitchChanged(SignalDoorSwitchChangedMessage message);
}
