using System.Net.Http.Json;
using System.Text;
using CabinetOs.Core.Utils;
using CabinetOs.Model.Dtos.Scada.Commands;
using static CabinetOs.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.ScadaCommandGateway;

/// <summary>
/// <see cref="IScadaCommandGateway"/>'in HTTP implementasyonu — kumandanin
/// gercekten sahaya gittigi yer.
///
/// <b>TEKRAR DENEME YOK.</b> Tekrarlanan bir role darbesi, basarisiz bir
/// komuttan daha kotudur: ikincisi gorunur bir hatadir, birincisi kilidi iki kez
/// acar ya da sireni iki kez calistirir. Zaman asiminda kullanici ACIKCA yeniden
/// dener. Bu yuzden ne Polly ne de <c>AddStandardResilienceHandler</c> takilidir;
/// eklenmesi sessiz bir davranis degisikligi olur.
///
/// Sozlesme: <c>docs/api-contract/08-scada-command.md</c>
/// </summary>
public class ScadaCommandGateway : IScadaCommandGateway
{
    /// <summary>
    /// Named client. Program.cs'te <c>AddHttpClient("scada")</c> ile kayitli;
    /// yeni <c>HttpClient</c> kurmak yerine fabrika kullanmanin sebebi soket
    /// tuketimi degil DNS: uzun omurlu tek bir <c>HttpClient</c>, SCADA'nin IP'si
    /// degistiginde eski adrese baglanmaya devam eder.
    /// </summary>
    public const string HttpClientName = "scada";

    /// <summary>SCADA tarafindaki yol. <c>Cabinet.ScadaBaseUrl</c>'in altina eklenir.</summary>
    private const string CommandPath = "command";

    /// <summary>
    /// <c>ResultMessage</c>'a yazilacak govdenin ust siniri. Sinirsiz okumak,
    /// hata sayfasi olarak megabaytlarca HTML donduren bir SCADA'nin veritabanina
    /// o HTML'i yazmasi demekti.
    /// </summary>
    private const int MaxMessageBytes = 512;

    private readonly IHttpClientFactory _httpClientFactory;

    public ScadaCommandGateway(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ScadaCommandOutcome> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout)
    {
        // Zaman asimi BU METODUN ici zamanlayicisiyla uygulanir, cagiranin
        // token'iyla degil (bkz. arayuzdeki gerekce). HttpClient.Timeout yerine
        // CTS kullanilmasinin sebebi ayirt edilebilirlik: HttpClient.Timeout da
        // TaskCanceledException firlatir ve "SCADA yavas" ile "istek iptal edildi"
        // ayni istisnaya duser.
        using var timeoutSource = new CancellationTokenSource(timeout);
        var timeoutToken = timeoutSource.Token;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            // Govde REST yanitlariyla AYNI serializer'dan gecer: SCADA ekibine
            // verilen ornekler camelCase ve enum'lar sayisal.
            using var response = await client.PostAsJsonAsync(
                BuildUrl(baseUrl),
                envelope,
                ProjectJsonOptions.SerializerOptions,
                timeoutToken);

            string? body = await ReadBoundedAsync(response.Content, timeoutToken);

            // 2xx = SCADA komutu KABUL ETTI. "Uyguladi" demek degildir; cikisin
            // gercekten degistigi ancak telemetriyle (ChannelValueChange) anlasilir.
            if (response.IsSuccessStatusCode)
                return new ScadaCommandOutcome(CommandStatus.Succeeded, body);

            // 4xx ve 5xx AYNI kovaya duser (Failed) cunku ikisi de "SCADA cevap
            // verdi ve komutu almadi" demektir. Ayrimi govde tasir; operatorun
            // yapacagi sey (yeniden dene / yapilandirmayi duzelt) her ikisinde de
            // ancak o metni okuyarak belirlenir.
            return new ScadaCommandOutcome(
                CommandStatus.Failed,
                string.IsNullOrWhiteSpace(body)
                    ? $"SCADA HTTP {(int)response.StatusCode} döndü"
                    : $"SCADA HTTP {(int)response.StatusCode}: {body}");
        }
        catch (OperationCanceledException)
        {
            // Cagiranin token'i yok; buraya yalnizca KENDI zaman asimimiz dusebilir.
            return new ScadaCommandOutcome(
                CommandStatus.NoResponse,
                $"SCADA {timeout.TotalSeconds:0.#} sn içinde yanıt vermedi");
        }
        catch (HttpRequestException exception)
        {
            // Baglanti kurulamadi / DNS / TLS. SCADA'ya HIC ulasilamadi:
            // Failed'dan farkli, cunku komutun sahaya gidip gitmedigi bilinmiyor.
            return new ScadaCommandOutcome(
                CommandStatus.NoResponse,
                $"SCADA'ya ulaşılamadı: {Truncate(exception.Message)}");
        }
        catch (Exception exception)
        {
            // Beklenmeyen her sey (bozuk URL, serilestirme) BIZIM hatamizdir ve
            // NoResponse'a yazilamaz: NoResponse "SCADA sessiz" demektir ve
            // operatoru yanlis yere bakmaya yonlendirirdi.
            return new ScadaCommandOutcome(
                CommandStatus.Failed,
                $"Komut gönderilemedi: {Truncate(exception.Message)}");
        }
    }

    private static string BuildUrl(string baseUrl) => $"{baseUrl.TrimEnd('/')}/{CommandPath}";

    /// <summary>
    /// Govdeyi SINIRLI okur. <c>ReadAsStringAsync</c> once tamamini bellege alir
    /// ve ancak sonra kirpabilirdik — kirpma o noktada gec kalmis olurdu.
    /// </summary>
    private static async Task<string?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[MaxMessageBytes];
        int read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        if (read == 0) return null;

        // UTF-8 sinirindan kirpmak son karakteri bozabilir; teshis metni icin
        // kabul edilebilir bir bedel.
        string text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
        return text.Length == 0 ? null : text;
    }

    private static string Truncate(string text) =>
        text.Length <= MaxMessageBytes ? text : text[..MaxMessageBytes];
}
