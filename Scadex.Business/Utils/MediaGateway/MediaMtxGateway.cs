using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.MediaGateway;

public class MediaMtxGateway : IMediaGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MediaGatewaySettings _mediaGatewaySettings;
    private readonly ICameraProtocolProfileResolver _profileResolver;
    private readonly ILogger<MediaMtxGateway> _logger;

    public MediaMtxGateway(IHttpClientFactory httpClientFactory, IOptions<MediaGatewaySettings> settings, ICameraProtocolProfileResolver profileResolver, ILogger<MediaMtxGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _mediaGatewaySettings = settings.Value;
        _profileResolver = profileResolver;
        _logger = logger;
    }

    private HttpClient CreateConfiguredClient()
    {
        var httpClient = _httpClientFactory.CreateClient(IMediaGateway.HttpClientName);

        httpClient.BaseAddress = new Uri(_mediaGatewaySettings.ApiBaseUrl.TrimEnd('/') + "/");
        httpClient.Timeout = TimeSpan.FromMilliseconds(_mediaGatewaySettings.ApiTimeoutMs);

        return httpClient;
    }


    /// <inheritdoc/>
    public async Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default)
    {
        var rtspUrl = _profileResolver.Resolve(camera).BuildRtspUrl(camera, profile);

        var payload = new Dictionary<string, object?>
        {
            ["source"] = rtspUrl,
            // Talep uzerine baglan: izleyicisi olmayan bir kamera icin RTSP oturumu acik tutulmaz "SourceOnDemandCloseAfterSec" saniye sonra RTSP oturumu kapatilir.
            ["sourceOnDemand"] = true,
            ["sourceOnDemandCloseAfter"] = $"{_mediaGatewaySettings.SourceOnDemandCloseAfterSec}s",
            ["rtspTransport"] = _mediaGatewaySettings.RtspTransport
        };

        var pathName = IMediaGateway.LivePathName(camera.Id, profile);

        var httpClient = CreateConfiguredClient();

        try
        {
            using var getResponse = await httpClient.GetAsync($"v3/config/paths/get/{pathName}", cancellationToken);

            // path mevcut değil MediaMTX'e ekle
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return await ControlApiPathRequestAsync(httpClient, "add", pathName, payload, cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                // mevcut path bilgilerini al
                var current = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

                // ayarlar aynı devam etme başarılı dön
                if (MatchesDesiredConfig(current, payload))
                    return Result.Success();

                // ayarlar farklı, mevcut path'i değiştir
                return await ControlApiPathRequestAsync(httpClient, "replace", pathName, payload, cancellationToken);
            }

            return Result.Failure(description: $"Medya geçidi yolu okunamadı: {await ReadErrorAsync(getResponse, cancellationToken)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // NOT: Eklenmemiş olan CancelationTokenSource kendi zaman asimi ile iptal edilirse, OperationCanceledException fırlatır. Bu durumda, MediaMTX'in yanıt vermediğini varsayabiliriz.
            // HttpClient'in kendi zaman asimi. "Gecit sessiz" ile "gecit yok" ayirt edilebilsin diye ayri mesaj.
            _logger.LogError("Medya gecidi sorgusu {Timeout} sn icinde yanit vermedi ({BaseAddress})", httpClient.Timeout.TotalSeconds, httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            // Kamera degil, MediaMTX ayakta degil.  
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Medya gecidi yolu yazilirken beklenmeyen hata: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yapılandırılamadı.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default)
    {
        var httpClient = CreateConfiguredClient();

        try
        {
            using var response = await httpClient.DeleteAsync($"v3/config/paths/delete/{pathName}", cancellationToken);

            // 404 = path zaten yok. Hata olarak değerlendirmeye gerek yok.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                return Result.Success();

            return Result.Failure(description: $"Medya geçidi yolu silinemedi: {await ReadErrorAsync(response, cancellationToken)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Medya gecidi yolu silinemedi: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yolu silinemedi.");
        }
    }


    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MediaPathInfo>>> ListPathsAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = CreateConfiguredClient();

        try
        {
            // Iki liste AYRI seylerdir ve ikisi de gerekli:
            //  - v3/config/paths/list  -> yapilandirma (record bayragi burada) ve silinebilir olanlar
            //  - v3/paths/list         -> calisma zamani (izleyici sayisi burada)
            // Yalnizca birine bakmak, ya kayittaki yolu ya da izlenen yolu kacirir.
            var configItems = await ReadPathItemsAsync(httpClient, "v3/config/paths/list", cancellationToken);
            if (configItems is null)
                return Result<IReadOnlyList<MediaPathInfo>>.Failure(description: "Medya geçidi yapılandırma yolları okunamadı.");

            var runtimeItems = await ReadPathItemsAsync(httpClient, "v3/paths/list", cancellationToken) ?? [];

            var runtimeByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var item in runtimeItems)
            {
                if (item.TryGetProperty("name", out var runtimeName) && runtimeName.GetString() is { } key)
                    runtimeByName[key] = item;
            }

            var paths = new List<MediaPathInfo>(configItems.Count);

            foreach (var config in configItems)
            {
                if (!config.TryGetProperty("name", out var nameElement) || nameElement.GetString() is not { } name)
                    continue;

                bool recordEnabled = config.TryGetProperty("record", out var record) && record.ValueKind == JsonValueKind.True;

                int readerCount = 0;
                bool isReady = false;

                if (runtimeByName.TryGetValue(name, out var runtime))
                {
                    if (runtime.TryGetProperty("readers", out var readers) && readers.ValueKind == JsonValueKind.Array)
                        readerCount = readers.GetArrayLength();

                    isReady = runtime.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True;
                }

                paths.Add(new MediaPathInfo(name, IsConfigured: true, readerCount, recordEnabled, isReady));
            }

            return Result<IReadOnlyList<MediaPathInfo>>.Success(paths);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Medya gecidi yol listesi {Timeout} sn icinde yanit vermedi ({BaseAddress})", httpClient.Timeout.TotalSeconds, httpClient.BaseAddress);
            return Result<IReadOnlyList<MediaPathInfo>>.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", httpClient.BaseAddress);
            return Result<IReadOnlyList<MediaPathInfo>>.Failure(description: "Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Medya gecidi yol listesi okunurken beklenmeyen hata");
            return Result<IReadOnlyList<MediaPathInfo>>.Failure(description: "Medya geçidi yolları listelenemedi.");
        }
    }


    /// <inheritdoc/>
    public async Task<Result<MediaRuntimePath?>> GetRuntimePathAsync(string pathName, CancellationToken cancellationToken = default)
    {
        var httpClient = CreateConfiguredClient();

        try
        {
            using var response = await httpClient.GetAsync($"v3/paths/get/{pathName}", cancellationToken);

            // 404 = yol şu an yok (yayıncı gelmemiş ya da gitmiş) — hata değil.
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Result<MediaRuntimePath?>.Success(null);
            if (!response.IsSuccessStatusCode)
                return Result<MediaRuntimePath?>.Failure(description: $"Medya geçidi yolu okunamadı: {await ReadErrorAsync(response, cancellationToken)}");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            bool ready = body.TryGetProperty("ready", out var readyElement) && readyElement.ValueKind == JsonValueKind.True;
            int readers = body.TryGetProperty("readers", out var readersElement) && readersElement.ValueKind == JsonValueKind.Array ? readersElement.GetArrayLength() : 0;
            string? sourceType = null, sourceId = null;
            if (body.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object)
            {
                sourceType = source.TryGetProperty("type", out var type) ? type.GetString() : null;
                sourceId = source.TryGetProperty("id", out var id) ? id.GetString() : null;
            }

            return Result<MediaRuntimePath?>.Success(new MediaRuntimePath(pathName, ready, readers, sourceType, sourceId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Medya gecidi yolu okunamadi: {PathName}", pathName);
            return Result<MediaRuntimePath?>.Failure(description: "Medya geçidine ulaşılamıyor.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> KickPublisherAsync(string pathName, CancellationToken cancellationToken = default)
    {
        var pathResult = await GetRuntimePathAsync(pathName, cancellationToken);
        if (!pathResult.IsSuccess)
            return Result.Failure(description: pathResult.Error.Description);
        if (pathResult.Data is not { SourceId: { } sourceId } path)
            return Result.Success();

        // Yayıncı türüne göre kick ucu; Scadex'in yayıncıları yalnızca RTSP (RTSPS açılırsa ikincisi).
        string? route = path.SourceType switch
        {
            "rtspSession" => "v3/rtspsessions/kick/",
            "rtspsSession" => "v3/rtspssessions/kick/",
            _ => null
        };
        if (route is null)
            return Result.Failure(description: $"Bu yayıncı türü atılamıyor: {path.SourceType}");

        var httpClient = CreateConfiguredClient();
        try
        {
            using var response = await httpClient.PostAsync(route + sourceId, null, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                return Result.Success();

            return Result.Failure(description: $"Yayıncı atılamadı: {await ReadErrorAsync(response, cancellationToken)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Medya gecidi yayincisi atilamadi: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidine ulaşılamıyor.");
        }
    }


    #region Helpers
    /// <summary>
    /// Control API'nin sayfali liste uclarini bastan sona okur.
    /// <c>itemCount</c> sayfa basina sinirli oldugu icin TEK istek yetmez;
    /// eksik okunan bir sayfa, temizlikte "izleyicisi yok" sanilan bir yola yol acardi.
    /// </summary>
    private static async Task<List<JsonElement>?> ReadPathItemsAsync(HttpClient httpClient, string route, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();

        for (int page = 0; ; page++)
        {
            using var response = await httpClient.GetAsync($"{route}?page={page}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            if (body.TryGetProperty("items", out var pageItems) && pageItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pageItems.EnumerateArray())
                    items.Add(item.Clone());
            }

            int pageCount = body.TryGetProperty("pageCount", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : 1;
            if (page + 1 >= pageCount) break;
        }

        return items;
    }

    private async Task<Result> ControlApiPathRequestAsync(HttpClient httpClient, string verb, string pathName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync($"v3/config/paths/{verb}/{pathName}", payload, cancellationToken);
        if (response.IsSuccessStatusCode)
            return Result.Success();

        return Result.Failure(description: $"Medya geçidi yolu yazılamadı: {await ReadErrorAsync(response, cancellationToken)}");
    }

    /// <summary>
    /// MediaMtx geçidindeki path ile gondermek istedigimiz path alanlarınıyla aynı değerlerde mi?
    /// Camera ip, port, kullanıcı adı, şifre gibi bilgiler veritabanında degistiginde hedef path bulunsa bile MediaMtx'de güncellenir tutarlılık sağlanır.
    /// Aksi takdirde MediaMTX eski bilgilerle baglanmaya calisir ve zaman asimina duserek baglantiyi keser.
    /// </summary>
    private static bool MatchesDesiredConfig(JsonElement current, Dictionary<string, object?> desired)
    {
        foreach (var (key, expected) in desired)
        {
            // mevcut path'te göndermek istediğimiz bir alan yoksa eşleşme yok demektir.
            if (!current.TryGetProperty(key, out var actual))
                return false;

            bool same = expected switch
            {
                string text => actual.ValueKind == JsonValueKind.String && actual.GetString() == text,
                bool flag => actual.ValueKind == (flag ? JsonValueKind.True : JsonValueKind.False),
                null => actual.ValueKind == JsonValueKind.Null,
                _ => false
            };

            if (!same) return false;
        }

        return true;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string trimmed = body.Trim();

        if (trimmed.Length == 0) return $"HTTP {(int)response.StatusCode}";
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }
    #endregion
}
