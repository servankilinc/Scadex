using System.Security.Cryptography;
using System.Text;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

/// <summary>
/// Canli izleme yolu — bilet uretimi ve dogrulamasi.
///
/// <b>Tarayici kameraya ASLA dogrudan baglanmaz.</b> Zincir soyle:
/// <list type="number">
/// <item>Istemci bilet ister; sunucu medya gecidinde yolu kurar ve opak bir
/// bilet uretip onbellege koyar.</item>
/// <item>Istemci SDP teklifini gecide gonderir, bileti
/// <c>Authorization: Basic base64("ticket:" + bilet)</c> olarak tasiyarak.</item>
/// <item>Gecit bileti bize sorar (<c>MediaGatewayController</c>), onay alirsa
/// kameraya baglanip goruntuyu istemciye WebRTC ile verir.</item>
/// </list>
/// Goruntu hicbir asamada ASP.NET uzerinden gecmez.
/// </summary>
public partial class CameraService
{
    public async Task<Result<StreamTokenDto>> CreateStreamTokenAsync(
    Guid cameraId, StreamProfile profile, CancellationToken cancellationToken = default)
    {
        var camera = await _unitOfWork.Cameras.GetAsync(
            where: c => c.Id == cameraId,
            cancellationToken: cancellationToken);

        if (camera == null)
            return Result<StreamTokenDto>.NotFound(description: "Kamera bulunamadi");

        if (!camera.IsActive)
            return StreamValidationProblem<StreamTokenDto>("IsActive", "Pasif kamera izlenemez.");

        // Kapali bir akim icin yol kurmak, MediaMTX'in var olmayan bir kanala
        // baglanmaya calisip zaman asimina dusmesi demekti. Sebep burada acikca
        // soyleniyor; istemci "baglanamadi" yerine ne yapmasi gerektigini gorur.
        bool enabled = profile == StreamProfile.Main ? camera.MainStreamEnabled : camera.SubStreamEnabled;
        if (!enabled)
        {
            string field = profile == StreamProfile.Main ? "MainStreamEnabled" : "SubStreamEnabled";
            string label = profile == StreamProfile.Main ? "Ana akım" : "Tali akım";
            return StreamValidationProblem<StreamTokenDto>(field, $"{label} bu kamerada kapalı.");
        }

        var ensureResult = await _mediaGateway.EnsureLivePathAsync(camera, profile, cancellationToken);
        if (!ensureResult.IsSuccess)
            return Result<StreamTokenDto>.Failure(description: ensureResult.Error.Description);

        string pathName = IMediaGateway.LivePathName(camera.Id, profile);

        // 32 bayt kriptografik rastgelelik. GUID KULLANILMAZ: Guid.NewGuid()
        // rastgeledir ama tahmin edilemezlik garantisi VERMEZ ve 6 biti surum/
        // varyant icin sabittir — bir yetkilendirme sirri icin yanlis arac.
        string ticket = Base64Url(RandomNumberGenerator.GetBytes(32));

        var ttl = TimeSpan.FromSeconds(_mediaMtxSettings.TicketTtlSeconds);
        var expiresAt = DateTime.UtcNow.Add(ttl);

        await _cache.SetStringAsync(
            TicketKeyPrefix + pathName + "_" + ticket,
            pathName,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);

        return Result<StreamTokenDto>.Success(new StreamTokenDto
        {
            WhepUrl = $"{_mediaMtxSettings.WebRtcPublicBaseUrl.TrimEnd('/')}/{pathName}/whep",
            Token = ticket,
            ExpirationUtc = expiresAt
        });
    }

    /// <summary>
    /// Medya gecidinin sordugu bileti dogrular.
    ///
    /// <b>Bilet TTL icinde COK KULLANIMLIKTIR.</b> Tek kullanimlik yapmak cazip
    /// gorunuyor ama yanlis olurdu: MediaMTX bir WHEP oturumu boyunca kancayi
    /// birden fazla kez cagirabilir ve ikinci cagri reddedilseydi oturum
    /// ortasindan kopardi. Guvenligi saglayan sey tek kullanimlik olmasi degil,
    /// YOLA BAGLI ve KISA OMURLU olmasi.
    /// </summary>
    public async Task<bool> ValidateStreamTokenAsync(string? path, string? ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(ticket)) return false;

        string? storedPath = await _cache.GetStringAsync(TicketKeyPrefix + path + "_" + ticket, cancellationToken);
        if (storedPath == null) return false;

        // Asil koruma yukaridaki onbellek iskasi. Bu karsilastirma ek bir kemer:
        // sabit zamanli olmasi, esitligin ne kadar surdugune bakarak bilgi
        // sizdirmayi engeller.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(path),
            Encoding.UTF8.GetBytes(storedPath));
    }


    /// <summary>
    /// Bilet onbellek anahtarinin oneki.
    ///
    /// <b>Anahtar YOLU ICERIR</b> ve IDOR savunmasi tam olarak buna dayanir:
    /// A kamerasi icin uretilmis bir bilet, B kamerasinin yoluyla arandiginda
    /// onbellekte bulunamaz. Bilet ile yol ayri saklansaydi, "bu bilet gecerli
    /// mi" sorusu "bu bilet BU YOL icin mi" sorusundan ayrisir ve ikincisini
    /// unutmak sessiz bir yetki acigi olurdu.
    /// </summary>
    private const string TicketKeyPrefix = "stream_ticket_";

    /// <summary>
    /// Kameranin canli yollarini gecitten dusurur.
    ///
    /// Sonuc BILEREK yutuluyor: bu bir temizliktir, cagiranin asil isinin
    /// (guncelleme) basarisini belirlememeli.
    /// </summary>
    private async Task RemoveLivePathsAsync(Camera camera, CancellationToken cancellationToken)
    {
        foreach (var profile in new[] { StreamProfile.Main, StreamProfile.Sub })
        {
            var result = await _mediaGateway.DeletePathAsync(
                IMediaGateway.LivePathName(camera.Id, profile), cancellationToken);

            if (!result.IsSuccess)
                _logger.LogWarning(
                    "Kamera {CameraId} icin {Profile} yolu medya gecidinden silinemedi: {Reason}",
                    camera.Id, profile, result.Error.Description);
        }
    }

    /// <summary>
    /// URL'de guvenle tasinabilen Base64. Duz Base64'teki <c>+</c>, <c>/</c> ve
    /// <c>=</c> karakterleri HTTP basliginda ve sorgu dizesinde ayrica
    /// kodlanmak zorunda kalirdi.
    /// </summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

}
