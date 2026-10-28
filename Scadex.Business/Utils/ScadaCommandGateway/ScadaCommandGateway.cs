using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Scadex.Core.Utils;
using Scadex.Model.Dtos.Scada.Commands;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.ScadaCommandGateway;

public class ScadaCommandGateway : IScadaCommandGateway
{
    // HttpClientFactory kullanmanin sebebi, uzun omurlu tek bir <c>HttpClient</c>, SCADA'nin IP'si degistiginde eski adrese baglanmaya devam eder.
    private readonly IHttpClientFactory _httpClientFactory;
    private const int MaxMessageLength = 512;

    /// <summary> Okuma yanıtının üst sınırı: 24 çıkış + girişler birkaç KB tutar; sınırsız okuma bozuk bir kartın belleği doldurmasına izin verirdi. </summary>
    private const int MaxStatusBodyBytes = 128 * 1024;


    public ScadaCommandGateway(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;


    public async Task<ScadaCommandResponse> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout)
    {
        // HttpClient.Timeout da TaskCanceledException firlatir ve "SCADA yavas" ile "istek iptal edildi" ayni hataya düşer.
        using var timeoutSource = new CancellationTokenSource(timeout);
        var timeoutToken = timeoutSource.Token;

        try
        {
            var client = _httpClientFactory.CreateClient(IScadaCommandGateway.HttpClientName);

            // POST {ScadaBaseUrl}/command + { channelNumber, value } (Docs/scada-http-api.md § 3)
            using var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/command",
                new ScadaCommandRequest(envelope.ChannelNumber, envelope.Value),
                ProjectJsonOptions.SerializerOptions,
                timeoutToken
            );

            // en fazla 512 byte okuyup mesajı kısaltacağız. SCADA'nın uzun hata mesajları olabilir.
            string? body = await ReadMessageAsync(response, timeoutToken);

            // 2xx = SCADA komutu KABUL ETTI.
            if (response.IsSuccessStatusCode)
                return new ScadaCommandResponse(CommandStatus.Succeeded, body);

            // 4xx ve 5xx Failed
            return new ScadaCommandResponse(CommandStatus.Failed, FailureMessage(response, body));
        }
        catch (OperationCanceledException)
        {
            // Çağıranın token'ı yok; buraya yalnizca KENDI zaman aşımımız dusebilir.
            return new ScadaCommandResponse(CommandStatus.NoResponse, TimeoutMessage(timeout));
        }
        catch (HttpRequestException exception)
        {
            // Baglanti kurulamadi
            return new ScadaCommandResponse(CommandStatus.NoResponse, UnreachableMessage(exception));
        }
        catch (Exception exception)
        {
            return new ScadaCommandResponse(CommandStatus.Failed, $"Komut gönderilemedi: {exception.Message.Truncate(MaxMessageLength)}");
        }
    }

    /// <inheritdoc />
    public async Task<ScadaStatusResponse> ReadStatusAsync(string baseUrl, IReadOnlyCollection<ScadaChannelRef>? channels, TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        var timeoutToken = timeoutSource.Token;

        try
        {
            var client = _httpClientFactory.CreateClient(IScadaCommandGateway.HttpClientName);
            string url = $"{baseUrl.TrimEnd('/')}/status";

            // GET = tüm kanallar, POST = gövdedeki kanallar (Docs/scada-http-api.md § 4)
            using var response = channels == null
                ? await client.GetAsync(url, timeoutToken)
                : await client.PostAsJsonAsync(url, channels, ProjectJsonOptions.SerializerOptions, timeoutToken);

            if (!response.IsSuccessStatusCode)
                return new ScadaStatusResponse(CommandStatus.Failed, null, FailureMessage(response, await ReadMessageAsync(response, timeoutToken)));

            var (buffer, read) = await ReadBodyAsync(response, MaxStatusBodyBytes + 1, timeoutToken);
            if (read > MaxStatusBodyBytes)
                return new ScadaStatusResponse(CommandStatus.Failed, null, $"SCADA okuma yanıtı {MaxStatusBodyBytes / 1024} KB sınırını aşıyor");

            List<ScadaChannelValue>? values;
            try
            {
                values = JsonSerializer.Deserialize<List<ScadaChannelValue>>(buffer.AsSpan(0, read), ProjectJsonOptions.SerializerOptions);
            }
            catch (JsonException exception)
            {
                return new ScadaStatusResponse(CommandStatus.Failed, null, $"SCADA okuma yanıtı çözülemedi: {exception.Message.Truncate(MaxMessageLength)}");
            }

            return values == null
                ? new ScadaStatusResponse(CommandStatus.Failed, null, "SCADA okuma yanıtı boş")
                : new ScadaStatusResponse(CommandStatus.Succeeded, values, null);
        }
        catch (OperationCanceledException)
        {
            return new ScadaStatusResponse(CommandStatus.NoResponse, null, TimeoutMessage(timeout));
        }
        catch (HttpRequestException exception)
        {
            return new ScadaStatusResponse(CommandStatus.NoResponse, null, UnreachableMessage(exception));
        }
        catch (Exception exception)
        {
            return new ScadaStatusResponse(CommandStatus.Failed, null, $"Okuma yapılamadı: {exception.Message.Truncate(MaxMessageLength)}");
        }
    }

    #region Helpers
    /// <summary> Gövdenin en fazla <paramref name="maxBytes"/> baytı; kalanı okunmaz. </summary>
    private static async Task<(byte[] Buffer, int Read)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes];
        int read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        return (buffer, read);
    }

    /// <summary> Gövdenin ilk 512 baytı metin olarak; boşsa <c>null</c>. </summary>
    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var (buffer, read) = await ReadBodyAsync(response, MaxMessageLength, cancellationToken);
        if (read == 0)
            return null;

        string text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
        return text.Length == 0 ? null : text;
    }

    private static string FailureMessage(HttpResponseMessage response, string? body) =>
        $"SCADA HTTP {(int)response.StatusCode}: detay: {body ?? "Bilinmeyen hata"}";

    private static string TimeoutMessage(TimeSpan timeout) => $"SCADA {timeout.TotalSeconds:0.#} sn içinde yanıt vermedi";

    private static string UnreachableMessage(HttpRequestException exception) => $"SCADA'ya ulaşılamadı: {exception.Message.Truncate(MaxMessageLength)}";
    #endregion
}
