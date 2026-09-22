namespace Scadex.Signalization.Realtime;

/// <summary>
/// Sinyalizasyon canli yayini. Cekirdegin <c>IDiagramNotifier</c>'i ile ayni kalip; uygulama hata FIRLATMAZ — yayin basarisizligi
/// yayini yapan kaydi bozmamali.
/// </summary>
public interface ISignalizationNotifier
{
    /// <summary> Bir operator islemi eklendi ya da guncellendi. Kaydin BASARILI olmasindan sonra cagrilir. </summary>
    Task OperatorSessionChangedAsync(OperatorSessionChangedMessage message, CancellationToken cancellationToken = default);

    /// <summary> Kabinin bir cikisi (siren / aydinlatma / kilit) degisti. Kanal degeri cekirdekte db ye YAZILDIKTAN sonra cagrilir. </summary>
    Task SignalCabinetStateChangedAsync(SignalCabinetStateChangedMessage message, CancellationToken cancellationToken = default);

    /// <summary> Bir kapinin anahtari degisti. Kanal degeri cekirdekte YAZILDIKTAN sonra cagrilir. </summary>
    Task SignalDoorSwitchChangedAsync(SignalDoorSwitchChangedMessage message, CancellationToken cancellationToken = default);
}
