using System.Globalization;
using Microsoft.Extensions.Options;
using Scadex.Business.Abstract;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CameraCaptureGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;

namespace Scadex.WebAPI.Utils;

/// <summary>
/// Anlik goruntu ve klip: <c>Kamera → MediaMTX (cam_{id}_main) → FFmpeg → JPEG / MP4</c>. Kaynak adresini (token, yerel) <c>CameraService</c> hazırlar; bu class kamerayı da markayı da bilmez
/// </summary>
public sealed partial class CameraCaptureGateway : ICameraCaptureGateway
{
    /// <summary> FFmpeg exe'nin tam yolu: <c>MediaGatewaySettings.FfmpegPath</c>, relative ise <see cref="IWebHostEnvironment.ContentRootPath"/>'e gore. </summary>
    private readonly string _ffmpegPath;
    private readonly ICameraCaptureSettingService _cameraCaptureSettingService;
    private readonly ILogger<CameraCaptureGateway> _logger;

    public CameraCaptureGateway(IWebHostEnvironment environment, IOptions<MediaGatewaySettings> mediaGatewaySettings, ICameraCaptureSettingService cameraCaptureSettingService, ILogger<CameraCaptureGateway> logger)
    {
        // Path.Combine ikinci arguman mutlaksa onu dondurur: ayarda mutlak yol da verilebilir.
        _ffmpegPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, mediaGatewaySettings.Value.FfmpegPath));
        _cameraCaptureSettingService = cameraCaptureSettingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<SnapshotPayload>> GetSnapshotAsync(Camera camera, string sourceUrl, CancellationToken cancellationToken = default)
    {
        var settings = await _cameraCaptureSettingService.GetSettingsAsync(cancellationToken);

        string[] arguments =
        [
            .. InputArguments(sourceUrl, settings.SnapshotTimeoutMs),
            // Ilk video karesi (FFmpeg anahtar kareyi bekler) tek bir JPEG olarak stdout'a.
            "-map", "0:v:0", "-frames:v", "1", "-an",
            "-c:v", "mjpeg", "-q:v", "2",
            "-f", "image2pipe", "pipe:1"
        ];

        // Anlik goruntude surecin tamami baglanti + ilk kare suresiyle sinirli.
        var timeout = TimeSpan.FromMilliseconds(settings.SnapshotTimeoutMs + KillMarginMs);
        var run = await RunFfmpegAsync(arguments, timeout, cancellationToken);

        var failure = DescribeFailure(run, camera, "Anlık görüntü", $"Kamera {settings.SnapshotTimeoutMs / 1000.0:0.#} sn içinde görüntü vermedi.");
        if (failure != null)
            return Result<SnapshotPayload>.Failure(description: failure.Message);

        if (run.Output.Length == 0)
            return Result<SnapshotPayload>.Failure(description: "Kamera görüntü göndermedi.");

        return Result<SnapshotPayload>.Success(new SnapshotPayload(run.Output, "image/jpeg"));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// IKI ADIM. FFmpeg <c>-t</c>'yi kameradan gelen ILK PAKETTEN sayar, ama <c>-c copy</c> yazmaya ilk ANAHTAR KAREDE baslar;
    /// aradaki bekleme klipten duserdi (I-kare araligi 2 sn olan kamerada 5 sn'lik klip ~4 sn cikiyordu). Bu yuzden:
    /// <list type="number">
    /// <item>Kaynaktan istenen sure + baglanti/ilk kare payi (<c>SnapshotTimeoutMs</c>) kadar ham kayit alinir.</item>
    /// <item>Ham dosya anahtar kareyle basladigi icin <c>-t</c> burada tam ilk kareden sayar; tam sureye kirpilip MP4'e paketlenir (yerel, yeniden kodlama yok).</item>
    /// </list>
    /// </remarks>
    public async Task<Result> RecordClipAsync(Camera camera, string sourceUrl, int durationSec, string outputFullPath, CancellationToken cancellationToken = default)
    {
        var settings = await _cameraCaptureSettingService.GetSettingsAsync(cancellationToken);

        string rawPath = Path.Combine(Path.GetDirectoryName(outputFullPath)!, Path.GetFileNameWithoutExtension(outputFullPath) + ".raw.mkv");
        long recordMs = durationSec * 1000L + settings.SnapshotTimeoutMs;

        try
        {
            // 1) Kaynaktan (MediaMTX canli yolu) ham kayit.
            string[] recordArguments =
            [
                "-y",
                .. InputArguments(sourceUrl, settings.SnapshotTimeoutMs),
                // Yeniden kodlama YOK (sahada yalniz H.264): -c copy ilk anahtar kareye kadarki kareleri atlar, klip gri baslamaz.
                "-map", "0:v:0", "-c:v", "copy", "-an",
                "-t", Seconds(recordMs),
                "-f", "matroska", rawPath
            ];

            // Sert sinir = baglanti + ham kayit suresi + dosyayi kapatma payi.
            var recordTimeout = TimeSpan.FromMilliseconds(settings.SnapshotTimeoutMs + recordMs + settings.ClipFinalizeGraceMs + KillMarginMs);
            var recordRun = await RunFfmpegAsync(recordArguments, recordTimeout, cancellationToken);

            var recordFailure = DescribeFailure(recordRun, camera, "Klip", "Klip kaydı zamanında tamamlanmadı; kamera görüntü göndermeyi kesmiş olabilir.");
            if (recordFailure != null)
                return Result.Failure(description: recordFailure.Message);

            if (!File.Exists(rawPath) || new FileInfo(rawPath).Length == 0)
                return Result.Failure(description: "Kamera görüntü göndermedi; klip dosyası oluşmadı.");

            // 2) Tam sureye kirp + MP4. moov atomu basa (faststart): tarayici dosyanin tamamini indirmeden oynatir.
            string[] trimArguments =
            [
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-i", rawPath,
                "-map", "0:v:0", "-c:v", "copy",
                "-t", durationSec.ToString(CultureInfo.InvariantCulture),
                "-movflags", "+faststart",
                "-f", "mp4", outputFullPath
            ];

            var trimRun = await RunFfmpegAsync(trimArguments, RemuxTimeout, cancellationToken);

            var trimFailure = DescribeFailure(trimRun, camera, "Klip (kirpma)", "Klip dosyası zamanında hazırlanamadı.", genericMessage: "Klip dosyası hazırlanamadı.");
            if (trimFailure != null)
                return Result.Failure(description: trimFailure.Message);

            if (!File.Exists(outputFullPath) || new FileInfo(outputFullPath).Length == 0)
                return Result.Failure(description: "Klip dosyası hazırlanamadı.");

            return Result.Success();
        }
        finally
        {
            TryDeleteFile(rawPath);
        }
    }

    #region Helpers
    /// <summary>
    /// Iki islemin ortak giris kismi. FFmpeg → MediaMTX baglantisi yereldir ve HEP <c>tcp</c>'dir. 
    /// Kamera → MediaMTX tasimasi ayri: <c>MediaGatewaySettings.RtspTransport</c>. <c>-timeout</c> RTSP soket G/C zaman asimidir (mikrosaniye);
    /// kimse izlemiyorken MediaMTX kameraya baglanip ilk kareyi getirene kadar okuma bu sureyle sinirlidir.     
    /// </summary>
    private static string[] InputArguments(string sourceUrl, int ioTimeoutMs) =>
    [
        "-hide_banner", "-loglevel", "error", "-nostdin",
        "-rtsp_transport", "tcp",
        "-timeout", (ioTimeoutMs * 1000L).ToString(CultureInfo.InvariantCulture),
        // Giris secenekleri -i'den ONCE gelmek zorunda; sonra yazilirsa cikisa uygulanir.
        "-i", sourceUrl
    ];

    private static string Seconds(long milliseconds) => (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Gecici klip dosyasi silinemedi: {Path}", path);
        }
    }
    #endregion
}
