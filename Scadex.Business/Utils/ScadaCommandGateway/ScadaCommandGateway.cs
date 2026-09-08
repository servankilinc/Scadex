using System.Net.Http.Json;
using System.Text;
using Scadex.Core.Utils;
using Scadex.Model.Dtos.Scada.Commands;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.ScadaCommandGateway;

public class ScadaCommandGateway : IScadaCommandGateway
{
    // HttpClientFactory kullanmanin sebebi, uzun omurlu tek bir <c>HttpClient</c>, SCADA'nin IP'si degistiginde eski adrese baglanmaya devam eder.
    private readonly IHttpClientFactory _httpClientFactory;
    private const int MaxMessageLength = 512;


    public ScadaCommandGateway(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;


    public async Task<ScadaCommandResponse> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout)
    {
        // HttpClient.Timeout da TaskCanceledException firlatir ve "SCADA yavas" ile "istek iptal edildi" ayni hataya düşer.
        using var timeoutSource = new CancellationTokenSource(timeout);
        var timeoutToken = timeoutSource.Token;

        try
        {
            #region Http request and read body
            var client = _httpClientFactory.CreateClient(IScadaCommandGateway.HttpClientName);

            using var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/command", envelope, ProjectJsonOptions.SerializerOptions, timeoutToken);
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutToken);

            var buffer = new byte[512]; // en fazla 512 byte okuyup mesajı kısaltacağız. SCADA'nın uzun hata mesajları olabilir.
            string? body;
            int read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, timeoutToken);
            if (read == 0)
            {
                body = null;
            }
            else
            {
                string text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                body = text.Length == 0 ? null : text;
            }
            #endregion

            // 2xx = SCADA komutu KABUL ETTI.
            if (response.IsSuccessStatusCode)
                return new ScadaCommandResponse(CommandStatus.Succeeded, body);

            // 4xx ve 5xx Failed
            return new ScadaCommandResponse(CommandStatus.Failed, $"SCADA HTTP {(int)response.StatusCode}: detay: {body ?? "Bilinmeyen hata"}");
        }
        catch (OperationCanceledException)
        {
            // Çağıranın token'ı yok; buraya yalnizca KENDI zaman aşımımız dusebilir.
            return new ScadaCommandResponse(CommandStatus.NoResponse, $"SCADA {timeout.TotalSeconds:0.#} sn içinde yanıt vermedi");
        }
        catch (HttpRequestException exception)
        {
            // Baglanti kurulamadi
            return new ScadaCommandResponse(CommandStatus.NoResponse, $"SCADA'ya ulaşılamadı: {exception.Message.Truncate(MaxMessageLength)}");
        }
        catch (Exception exception)
        {
            return new ScadaCommandResponse(CommandStatus.Failed, $"Komut gönderilemedi: {exception.Message.Truncate(MaxMessageLength)}");
        }
    }
}
