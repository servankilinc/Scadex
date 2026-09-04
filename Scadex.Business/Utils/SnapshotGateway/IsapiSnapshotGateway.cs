using Microsoft.Extensions.Logging;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Scadex.Business.Utils.SnapshotGateway;

public class IsapiSnapshotGateway : ISnapshotGateway
{
    public const string HttpClientName = "camera-snapshot";

    /// <summary>
    /// Kamera basina son gorulen challenge ve nonce bilgilerini onbellekte tutar. Her kamera icin tek challenge ve nonce sayaci vardir.
    /// Challenge onbellege alinmasaydi HER anlik goruntu iki gidis-gelis olurdu (401 al, imzala, tekrar gonder). Onbellekle normal durum tek istektir.
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
            return Result<SnapshotPayload>.Validation(new Dictionary<string, string[]> { ["Password"] = ["Kameranın kullanıcı adı ve parolası tanımlı değil"] }, description: "ISAPI kimlik doğrulaması için kullanıcı adı ve parola gerekiyor.");        

        string snapshotPath = _profileResolver.Resolve(camera).BuildSnapshotPath(camera);

        string url = $"http://{camera.IpAddress}:{camera.HttpPort}{snapshotPath}";

        // NOT: Zaman asimi ile HttpClient.Timeout da TaskCanceledException firlatir ve "kamera yavas" ile "istek iptal edildi" ayni istisnaya duserdi
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(_settings.SnapshotTimeoutMs));

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await SendWithAuthAsync(client, camera, url, snapshotPath, timeoutSource.Token);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Onbellekteki challenge artik gecersiz; bir sonraki istek yeniden senkronize etsin.
                _challengeCache.TryRemove(camera.Id, out _);
                return Result<SnapshotPayload>.Validation(new Dictionary<string, string[]> { ["Password"] = ["Kamera kimlik doğrulamayı reddetti. Kullanıcı adı veya parola yanlış."] }, description: "ISAPI kimlik doğrulaması başarısız oldu.");
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
            return Result<SnapshotPayload>.Failure(description: $"Kamera {_settings.SnapshotTimeoutMs / 1000.0:0.#} sn içinde yanıt vermedi.");
        }
        catch (HttpRequestException exception)
        {
            return Result<SnapshotPayload>.Failure(description: $"Kameraya ulaşılamadı: {exception.Message.Truncate(256)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Kamera {CameraId} anlik goruntusu alinirken beklenmeyen hata", camera.Id);
            return Result<SnapshotPayload>.Failure(description: "Anlık görüntü alınamadı.");
        }
    }

    /// <summary>
    /// Onbellekteki challenge ile dener; 401 gelirse yeni challenge'i alip BIR KEZ tekrar dener. Ikinci 401 gercek bir kimlik hatasidir.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpClient client, Camera camera, string url, string path, CancellationToken cancellationToken)
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

    /// <summary> Protokol baska bir algoritmaya izin vermiyor. </summary>
    private static string Md5(string input) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string? ExtractDirective(string challenge, string key)
    {
        var quoted = Regex.Match(challenge, $@"{key}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        if (quoted.Success) return quoted.Groups[1].Value;

        var bare = Regex.Match(challenge, $@"{key}\s*=\s*([^,\s]+)", RegexOptions.IgnoreCase);
        return bare.Success ? bare.Groups[1].Value : null;
    }


    /// <summary>Bir kameranin challenge'i ve o challenge icin nonce sayaci.</summary>
    private sealed class CachedChallenge(string challenge)
    {
        public string Challenge { get; } = challenge;

        private int _nonceCount;

        public int NextNonceCount() => Interlocked.Increment(ref _nonceCount);
    }
}
