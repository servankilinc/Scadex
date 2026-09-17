namespace Scadex.Signalization.Realtime;

/// <summary>
/// Sinyalizasyon canli yayini. Cekirdegin <c>IDiagramNotifier</c>'i ile ayni kalip; uygulama hata FIRLATMAZ — yayin basarisizligi
/// yayini yapan kaydi bozmamali.
/// </summary>
public interface ISignalizationNotifier
{
    /// <summary> Bir operator islemi eklendi ya da guncellendi. Kaydin BASARILI olmasindan sonra cagrilir. </summary>
    Task OperatorSessionChangedAsync(OperatorSessionChangedMessage message, CancellationToken cancellationToken = default);
}
