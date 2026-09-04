using Microsoft.Extensions.Logging;
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
    private readonly HttpClient _httpClient;
    private readonly MediaMtxSettings _settings;
    private readonly ICameraProtocolProfileResolver _profileResolver;
    private readonly ILogger<MediaMtxGateway> _logger;

    public MediaMtxGateway(HttpClient httpClient, MediaMtxSettings settings, ICameraProtocolProfileResolver profileResolver, ILogger<MediaMtxGateway> logger)
    {
        _settings = settings;
        _profileResolver = profileResolver;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds((_settings.ApiTimeoutMs/1000) + 10);
    }


    /// <inheritdoc/>
    public async Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default)
    {
        var rtspUrl = _profileResolver.Resolve(camera).BuildRtspUrl(camera, profile);

        var payload = new Dictionary<string, object?>
        {
            ["source"] = rtspUrl,
            // Talep uzerine baglan: izleyicisi olmayan bir kamera icin RTSP oturumu acik tutulmaz "SourceOnDemandCloseAfter" saniye sonra RTSP oturumu kapatilir.
            ["sourceOnDemand"] = true,
            ["sourceOnDemandCloseAfter"] = _settings.SourceOnDemandCloseAfter,
            ["rtspTransport"] = _settings.RtspTransport
        };

        var pathName = IMediaGateway.LivePathName(camera.Id, profile);

        try
        {
            using var getResponse = await _httpClient.GetAsync($"v3/config/paths/get/{pathName}", cancellationToken);

            // path mevcut değil MediaMTX'e ekle
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return await ControlApiPathRequestAsync("add", pathName, payload, cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                // mevcut path bilgilerini al
                var current = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

                // ayarlar aynı devam etme başarılı dön
                if (MatchesDesiredConfig(current, payload))
                    return Result.Success();

                // ayarlar farklı, mevcut path'i değiştir
                return await ControlApiPathRequestAsync("replace", pathName, payload, cancellationToken);
            }

            return Result.Failure(description: $"Medya geçidi yolu okunamadı: {await ReadErrorAsync(getResponse, cancellationToken)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // NOT: Eklenmemiş olan CancelationTokenSource kendi zaman asimi ile iptal edilirse, OperationCanceledException fırlatır. Bu durumda, MediaMTX'in yanıt vermediğini varsayabiliriz.
            // HttpClient'in kendi zaman asimi. "Gecit sessiz" ile "gecit yok" ayirt edilebilsin diye ayri mesaj.
            _logger.LogError("Medya gecidi sorgusu {Timeout} sn icinde yanit vermedi ({BaseAddress})", _httpClient.Timeout.TotalSeconds, _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            // Kamera degil, MediaMTX ayakta degil.  
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Medya gecidi yolu yazilirken beklenmeyen hata: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yapılandırılamadı.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> EnsureClipPathAsync(Camera camera, long captureId, string recordPath, string segmentDuration, CancellationToken cancellationToken = default)
    {
        // Video kaydı her zaman Main stream'den alinir: 
        var rtspUrl = _profileResolver.Resolve(camera).BuildRtspUrl(camera, StreamProfile.Main);

        var payload = new Dictionary<string, object?>
        {
            ["source"] = rtspUrl,
            // Talep BEKLENMEZ: kaydin hemen baslamasi gerekiyor, yoksa ilk izleyici gelene kadar hicbir sey yazilmaz.
            ["sourceOnDemand"] = false,
            ["rtspTransport"] = _settings.RtspTransport,
            ["record"] = true,
            ["recordPath"] = recordPath,
            // fmp4 -> tarayicinin dogrudan oynatabildigi .mp4 dosyasi.
            ["recordFormat"] = "fmp4",
            ["recordSegmentDuration"] = segmentDuration,
            // Otomatik silme KAPALI: dosyayi biz tasiyacagiz. MediaMTX'in silmesi, tam da okumaya calistigimiz dosyayi kaybettirebilirdi.
            ["recordDeleteAfter"] = "0s"
        };

        var pathName = IMediaGateway.ClipPathName(captureId);

        try
        {
            // Video kaydı path'i doğrudan yazilir, Ad her cekimde benzersiz (clip_{captureId})
            using var addResponse = await _httpClient.PostAsJsonAsync($"v3/config/paths/add/{pathName}", payload, cancellationToken);
            if (addResponse.IsSuccessStatusCode)
                return Result.Success();

            string addError = await ReadErrorAsync(addResponse, cancellationToken);

            if (!addError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Result.Failure(description: $"Medya geçidi yolu oluşturulamadı: {addError}");

            return await ControlApiPathRequestAsync("replace", pathName, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Medya gecidi {Timeout} sn icinde yanit vermedi ({BaseAddress})", _httpClient.Timeout.TotalSeconds, _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", _httpClient.BaseAddress);
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
        try
        {
            using var response = await _httpClient.DeleteAsync($"v3/config/paths/delete/{pathName}", cancellationToken);

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


    #region Helpers
    private async Task<Result> ControlApiPathRequestAsync(string verb, string pathName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync($"v3/config/paths/{verb}/{pathName}", payload, cancellationToken);
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
