using Microsoft.Extensions.Caching.Distributed;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Entities;
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
        // 1) Kamera bilgilerini al
        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == cameraId, cancellationToken: cancellationToken);

        if (camera == null)
            return Result<StreamTokenDto>.NotFound(description: "Kamera bulunamadi");

        if (!camera.IsActive)
            return Result<StreamTokenDto>.Validation(new Dictionary<string, string[]> { ["IsActive"] = ["Pasif kamera izlenemez."] });

        // Main strean olmayan markada Sub strean istegi Main yoluna duser. Ayri bir "_sub" yolu acilmaz ki kameraya
        // ayni akim icin ikinci bir RTSP baglantisi kurulmasin. Istemci farki gormez: dogru yol WHEP URL'inde gelir.
        var effectiveProfile = profile == StreamProfile.Sub && !_cameraProtocolProfileResolver.Resolve(camera).HasSubStream ? StreamProfile.Main : profile;

        // 2) Media Gateway'de path'i olustur
        var ensureResult = await _mediaGateway.EnsureLivePathAsync(camera, effectiveProfile, cancellationToken);
        if (!ensureResult.IsSuccess)
            return Result<StreamTokenDto>.Failure(description: ensureResult.Error.Description);

        // 3) aynı path ismini üret, token üret ve cache'e koy
        string pathName = IMediaGateway.LivePathName(camera.Id, effectiveProfile);
        var (streamToken, expiresAt) = await IssueStreamTokenAsync(pathName, cancellationToken);

        // 4) client'a don
        return Result<StreamTokenDto>.Success(new StreamTokenDto
        {
            WhepUrl = $"{_mediaGatewaySettings.WebRtcPublicBaseUrl.TrimEnd('/')}/{pathName}/whep",
            Token = streamToken,
            ExpirationUtc = expiresAt
        });
    }

    /// <summary>
    /// Yola bagli, kisa omurlu okuma token'ı uretir ve cache'e koyar; MediaMTX auth kancasi <see cref="ValidateStreamTokenAsync"/> ile dogrular.
    /// Hem tarayici (WHEP) hem sunucu ici cekim (FFmpeg, <c>PrepareCaptureSourceAsync</c>) ayni bileti kullanir: ikisi de ayni kapidan gecer.
    /// </summary>
    private async Task<(string Token, DateTime ExpiresAt)> IssueStreamTokenAsync(string pathName, CancellationToken cancellationToken)
    {
        // Base64 Dönüşümü ve URL-Safe (Güvenli) Hale Getirme
        var randomNumber = RandomNumberGenerator.GetBytes(32);
        string streamToken = Convert.ToBase64String(randomNumber).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var ttl = TimeSpan.FromSeconds(_mediaGatewaySettings.TokenTtlSeconds);

        await _cache.SetStringAsync(
            StreamTokenCacheKey(pathName, streamToken),
            pathName,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, cancellationToken
        );

        return (streamToken, DateTime.UtcNow.Add(ttl));
    }

    /// <summary>
    /// Cekim (anlik goruntu / klip) icin FFmpeg'in okuyacagi kaynak: MediaMTX'in main stream canlı path'i + token.
    /// Kameraya dogrudan baglanilmaz: izleme aciksa ayni oturum paylasilir, kimse izlemiyorsa MediaMTX oturumu
    /// <c>sourceOnDemandCloseAfter</c> boyunca acik tutar (giris serisinin sonraki kareleri yeniden baglanmaz).
    /// Token omru kisa olabilir: MediaMTX RTSP okumasinda auth'u yalnizca oturum kurulurken sorar.
    /// </summary>
    private async Task<Result<string>> PrepareCaptureSourceAsync(Camera camera, CancellationToken cancellationToken)
    {
        var ensureResult = await _mediaGateway.EnsureLivePathAsync(camera, StreamProfile.Main, cancellationToken);
        if (!ensureResult.IsSuccess)
            return Result<string>.Failure(description: ensureResult.Error.Description);

        string pathName = IMediaGateway.LivePathName(camera.Id, StreamProfile.Main);
        var (token, _) = await IssueStreamTokenAsync(pathName, cancellationToken);

        return Result<string>.Success(IMediaGateway.LiveRtspUrl(_mediaGatewaySettings.RtspPort, pathName, token));
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
