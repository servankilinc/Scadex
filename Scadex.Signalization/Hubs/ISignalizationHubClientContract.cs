using Scadex.Signalization.Realtime;

namespace Scadex.Signalization.Hubs;

/// <summary> Sinyalizasyon hub'inin Client arayuzu: Client'lara hangi mesajlarin gonderilebilecegini tanimlar. </summary>
public interface ISignalizationHubClientContract
{
    /// <summary> Bir operator islemi eklendi ya da guncellendi (acildi, kapandi, bayrak/siren/zamanlayici degisti). </summary>
    Task OperatorSessionChanged(OperatorSessionChangedMessage message);
}
