using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CabinetOs.Business.Settings;
using CabinetOs.Business.Utils.CameraProtocolProfile;
using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Entities;
using Microsoft.Extensions.Logging;

namespace CabinetOs.Business.Utils.SnapshotGateway;

/// <summary>
/// <see cref="ISnapshotGateway"/>'in ISAPI implementasyonu.
///
/// Kamera Digest ister ve <c>HttpClientHandler.Credentials</c> KULLANILAMAZ
/// (bkz. arayuzdeki gerekce: kimlik handler'a baglanir, handler havuzda
/// paylasilir). Bu yuzden imza elle uretiliyor.
/// </summary>
public class IsapiSnapshotGateway : ISnapshotGateway
{
    /// <summary>
    /// Named client. <c>Program.cs</c>'te sonsuz timeout ile kayitli; zaman
    /// asimini bu sinif kendi CTS'iyle uyguluyor.
    /// </summary>
    public const string HttpClientName = "camera-snapshot";

    /// <summary>
    /// Kamera basina son gorulen challenge ve nonce sayaci.
    ///
    /// Challenge onbellege alinmasaydi HER anlik goruntu iki gidis-gelis olurdu
    /// (401 al, imzala, tekrar gonder). Onbellekle normal durum tek istektir.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, CachedChallenge> _challengeCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICameraProtocolProfileResolver _profileResolver;
    private readonly CameraCaptureSettings _settings;
    private readonly ILogger<IsapiSnapshotGateway> _logger;

    public IsapiSnapshotGateway(
        IHttpClientFactory httpClientFactory,
        ICameraProtocolProfileResolver profileResolver,
        CameraCaptureSettings settings,
        ILogger<IsapiSnapshotGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _profileResolver = profileResolver;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result<SnapshotPayload>> GetSnapshotAsync(Camera camera, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(camera.Username) || string.IsNullOrEmpty(camera.Password))
            return CredentialProblem("Kameranın kullanıcı adı ve parolası tanımlı değil.");

        string path = _profileResolver.Resolve(camera).BuildSnapshotPath(camera);

        // HTTPS BILEREK KULLANILMIYOR: HttpsPort kolonu dursa da kameralarin
        // sertifikasi kendinden imzalidir ve dogrulamayi kapatmak, TLS'in
        // sagladigi tek seyi ortadan kaldirirdi. Kapali ag varsayimi geregi
        // duz HTTP kullaniliyor.
        string url = $"http://{camera.IpAddress}:{camera.HttpPort}{path}";

        // Zaman asimi CTS ile: HttpClient.Timeout da TaskCanceledException
        // firlatir ve "kamera yavas" ile "istek iptal edildi" ayni istisnaya
        // duserdi (ScadaCommandGateway ile ayni gerekce).
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(_settings.SnapshotTimeoutMs));

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await SendWithAuthAsync(client, camera, url, path, timeoutSource.Token);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Onbellekteki challenge artik gecersiz; bir sonraki istek
                // yeniden pazarlik etsin.
                _challengeCache.TryRemove(camera.Id, out _);
                return CredentialProblem("Kamera kimlik doğrulamayı reddetti. Kullanıcı adı veya parola yanlış.");
            }

            if (!response.IsSuccessStatusCode)
                return Result<SnapshotPayload>.Failure(description: $"Kamera anlık görüntü isteğine HTTP {(int)response.StatusCode} döndü.");

            byte[] content = await response.Content.ReadAsByteArrayAsync(timeoutSource.Token);
            if (content.Length == 0)
                return Result<SnapshotPayload>.Failure(description: "Kamera boş bir görüntü döndürdü.");

            string contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return Result<SnapshotPayload>.Success(new SnapshotPayload(content, contentType));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cagiranin token'i iptal edilmedi => buraya yalnizca KENDI zaman
            // asimimiz dusebilir.
            return Result<SnapshotPayload>.Failure(
                description: $"Kamera {_settings.SnapshotTimeoutMs / 1000.0:0.#} sn içinde yanıt vermedi.");
        }
        catch (HttpRequestException exception)
        {
            return Result<SnapshotPayload>.Failure(description: $"Kameraya ulaşılamadı: {Truncate(exception.Message)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Kamera {CameraId} anlik goruntusu alinirken beklenmeyen hata", camera.Id);
            return Result<SnapshotPayload>.Failure(description: "Anlık görüntü alınamadı.");
        }
    }

    /// <summary>
    /// Onbellekteki challenge ile dener; 401 gelirse yeni challenge'i alip
    /// BIR KEZ tekrar dener. Ikinci 401 gercek bir kimlik hatasidir.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(
        HttpClient client, Camera camera, string url, string path, CancellationToken cancellationToken)
    {
        using (var firstRequest = new HttpRequestMessage(HttpMethod.Get, url))
        {
            if (_challengeCache.TryGetValue(camera.Id, out var cached))
                firstRequest.Headers.Authorization = BuildAuthHeader(camera, path, cached);

            var response = await client.SendAsync(firstRequest, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            string? challenge = response.Headers.WwwAuthenticate.FirstOrDefault()?.ToString();
            response.Dispose();

            if (string.IsNullOrEmpty(challenge))
                return await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

            _challengeCache[camera.Id] = new CachedChallenge(challenge);
        }

        using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
        if (_challengeCache.TryGetValue(camera.Id, out var refreshed))
            retryRequest.Headers.Authorization = BuildAuthHeader(camera, path, refreshed);

        return await client.SendAsync(retryRequest, cancellationToken);
    }

    private AuthenticationHeaderValue? BuildAuthHeader(Camera camera, string path, CachedChallenge cached)
    {
        string username = camera.Username!;
        string password = camera.Password!;
        string challenge = cached.Challenge;

        if (challenge.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            return new AuthenticationHeaderValue("Basic", encoded);
        }

        if (!challenge.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
            return null;

        string? realm = ExtractDirective(challenge, "realm");
        string? nonce = ExtractDirective(challenge, "nonce");
        string? qop = ExtractDirective(challenge, "qop");
        string? opaque = ExtractDirective(challenge, "opaque");

        if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(nonce)) return null;

        // nonce-count HER ISTEKTE ARTAR. Sabit birakilsaydi (prototipte oyleydi)
        // onbellege alinmis bir challenge'i tekrar kullanmak, nonce sayacinin
        // artmasini bekleyen sikı firmware'de reddedilirdi — ve onbellegin
        // kazandirdigi tek gidis-gelis her seferinde kaybedilirdi.
        string nc = cached.NextNonceCount().ToString("x8");
        string cnonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

        string ha1 = Md5($"{username}:{realm}:{password}");
        // uri, SORGU DIZESI OLMADAN yalnizca yoldur — RFC 7616 boyle tanimliyor.
        string ha2 = Md5($"GET:{path}");

        string response;
        var builder = new StringBuilder();
        builder.Append($"username=\"{username}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{path}\"");

        if (!string.IsNullOrEmpty(qop) && qop.Contains("auth", StringComparison.OrdinalIgnoreCase))
        {
            response = Md5($"{ha1}:{nonce}:{nc}:{cnonce}:auth:{ha2}");
            builder.Append($", response=\"{response}\", qop=auth, nc={nc}, cnonce=\"{cnonce}\"");
        }
        else
        {
            response = Md5($"{ha1}:{nonce}:{ha2}");
            builder.Append($", response=\"{response}\"");
        }

        if (!string.IsNullOrEmpty(opaque))
            builder.Append($", opaque=\"{opaque}\"");

        return new AuthenticationHeaderValue("Digest", builder.ToString());
    }

    /// <summary>
    /// MD5 — Digest'in tanimi geregi. Genel amacli bir ozet secimi DEGILDIR;
    /// protokol baska bir algoritmaya izin vermiyor.
    /// </summary>
    private static string Md5(string input) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string? ExtractDirective(string challenge, string key)
    {
        var quoted = Regex.Match(challenge, $@"{key}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        if (quoted.Success) return quoted.Groups[1].Value;

        var bare = Regex.Match(challenge, $@"{key}\s*=\s*([^,\s]+)", RegexOptions.IgnoreCase);
        return bare.Success ? bare.Groups[1].Value : null;
    }

    private static string Truncate(string text) => text.Length <= 256 ? text : text[..256];

    /// <summary>
    /// Kimlik bilgisi sorunlari 400 doner, 500 DEGIL: hata bizde degil kameranin
    /// tanimindadir ve duzeltmesi kullanicinin elindedir. Sozluk anahtari
    /// <c>Camera</c> alan adlariyla ayni — sozlesme geregi PascalCase.
    /// </summary>
    private static Result<SnapshotPayload> CredentialProblem(string message) =>
        Result<SnapshotPayload>.Validation(
            new Dictionary<string, string[]> { ["Password"] = [message] },
            description: message);

    /// <summary>Bir kameranin challenge'i ve o challenge icin nonce sayaci.</summary>
    private sealed class CachedChallenge(string challenge)
    {
        public string Challenge { get; } = challenge;

        private int _nonceCount;

        public int NextNonceCount() => Interlocked.Increment(ref _nonceCount);
    }
}
