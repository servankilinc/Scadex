using Scadex.Model.Scada.Commands;

namespace Scadex.Business.Utils.ScadaCommandGateway;

public interface IScadaCommandGateway
{
    public const string HttpClientName = "http_client_scada";

    /// <summary>
    /// Kontrol kartının <c>ScadaCommandTimeoutMs</c>'inden karta yapılan isteğin zaman aşımı. Kullanıcının girdiği değere karşı ek güvenlik:
    /// 0 ya da 900K gibi değerler 5 sn – 3 dk aralığına çekilir. Komut ve okuma aynı sınırı kullanır.
    /// </summary>
    public static TimeSpan RequestTimeout(int cardTimeoutMs) => TimeSpan.FromMilliseconds(Math.Clamp(cardTimeoutMs, 5000, 180000));
    Task<ScadaCommandResponse> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout);

    /// <summary>
    /// Kartın kanal değerlerini okur: <paramref name="channels"/> <c>null</c> ise <c>GET /status</c> (tüm kanallar), doluysa
    /// <c>POST /status</c> (yalnızca istenenler). Yeniden deneme yoktur.
    /// </summary>
    Task<ScadaStatusResponse> ReadStatusAsync(string baseUrl, IReadOnlyCollection<ScadaChannelRef>? channels, TimeSpan timeout);
}
