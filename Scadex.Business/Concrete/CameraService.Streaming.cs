using Microsoft.Extensions.Caching.Distributed;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Queries;
using System.Security.Cryptography;
using System.Text;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

/// <summary>
/// Tarayici kameraya ASLA dogrudan baglanmaz. Zincir soyle:
/// <list type="number">
/// <item>Client token ister; sunucu Media Gateway path kurar ve token uretip cache koyar.</item>
/// <item>Client SDP teklifini gecide gonderir, token <c>Authorization: Basic base64("token:" + token)</c> olarak tasiyarak.</item>
/// <item>Media Gateway token'ı bize sorar (Control Api Authentication), onay alırsa bizden kameraya baglanip goruntuyu client'a WebRTC ile verir.</item>
/// </list>
/// </summary>
public partial class CameraService
{
    #region Cache Key 
    private string StreamTokenCacheKey(string path, string token) => "scadex_stream_token" + "_" + path + "_" + token;
    #endregion

    /// <inheritdoc/>
    public async Task<Result<StreamTokenDto>> CreateStreamTokenAsync(Guid cameraId, StreamProfile profile, CancellationToken cancellationToken = default)
    {
        // 1) Kamera bilgilerini al ve Stream profilinin aktif olup olmadigini kontrol et
        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == cameraId, cancellationToken: cancellationToken);

        if (camera == null)
            return Result<StreamTokenDto>.NotFound(description: "Kamera bulunamadi");

        if (!camera.IsActive)
            return Result<StreamTokenDto>.Validation(new Dictionary<string, string[]> { ["IsActive"] = ["Pasif kamera izlenemez."] });

        bool enabled = profile == StreamProfile.Main ? camera.MainStreamEnabled : camera.SubStreamEnabled;
        if (!enabled)
        {
            string field = profile == StreamProfile.Main ? "MainStreamEnabled" : "SubStreamEnabled";
            string label = profile == StreamProfile.Main ? "Main stream" : "Sub stream";

            return Result<StreamTokenDto>.Validation(new Dictionary<string, string[]> { [field] = [$"{label} bu kamerada kapalı."] });
        }

        // 2) Media Gateway'de path'i olustur
        var ensureResult = await _mediaGateway.EnsureLivePathAsync(camera, profile, cancellationToken);
        if (!ensureResult.IsSuccess)
            return Result<StreamTokenDto>.Failure(description: ensureResult.Error.Description);

        // 3) aynı path ismini üret, token ve TTL belirle
        string pathName = IMediaGateway.LivePathName(camera.Id, profile);


        // Base64 Dönüşümü ve URL-Safe (Güvenli) Hale Getirme
        var randomNumber = RandomNumberGenerator.GetBytes(32);
        string streamToken = Convert.ToBase64String(randomNumber).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var ttl = TimeSpan.FromSeconds(_mediaMtxSettings.TokenTtlSeconds);

        var expiresAt = DateTime.UtcNow.Add(ttl);

        string tokenKey = StreamTokenCacheKey(pathName, streamToken);

        // 4) Token'ı cache'e koy ve client'a don
        await _cache.SetStringAsync(
            tokenKey,
            pathName,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, cancellationToken
        );

        return Result<StreamTokenDto>.Success(new StreamTokenDto
        {
            WhepUrl = $"{_mediaMtxSettings.WebRtcPublicBaseUrl.TrimEnd('/')}/{pathName}/whep",
            Token = streamToken,
            ExpirationUtc = expiresAt
        });
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateStreamTokenAsync(string? path, string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(token))
            return false;

        string tokenKey = StreamTokenCacheKey(path, token);

        string? storedPath = await _cache.GetStringAsync(tokenKey, cancellationToken);

        if (string.IsNullOrEmpty(storedPath))
            return false;

        // byte byte karsilastirma ayrıca çok gerekli olmasa da(token süresi çok uzun değil zaten) timing attack'lara karşı güvenli
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(path), Encoding.UTF8.GetBytes(storedPath));
    }
}
