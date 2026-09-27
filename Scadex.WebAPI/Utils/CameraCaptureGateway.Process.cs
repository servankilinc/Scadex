using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Scadex.Model.Entities;

namespace Scadex.WebAPI.Utils;

public sealed partial class CameraCaptureGateway
{
    private const int MaxErrorLength = 4096;

    /// <summary> FFmpeg'in kendi RTSP <c>-timeout</c>'undan bu kadar SONRA gelir: bağlanamayan kamerada FFmpeg'in "Connection ... failed" hatası once duşmeli ki "ulaşılamadı" ile "görüntü vermedi" ayirt edilsin </summary>
    private const int KillMarginMs = 1000;

    /// <summary> Ham kaydi kırpan yerel remux icin sınır (kamera yok, yalnizca disk). En uzun klip 3600 sn'de bile saniyeler surer. </summary>
    private static readonly TimeSpan RemuxTimeout = TimeSpan.FromSeconds(60);

    /// <summary> Bir FFmpeg calistirmasinin sonucu. </summary>
    /// <param name="ExitCode">Zaman asiminda veya exe yokken <c>-1</c>.</param>
    /// <param name="Output">stdout baytlari (anlik goruntude JPEG; klipte bos).</param>
    /// <param name="Error">stderr (kirpilmis, MASKELENMEMIS — disari yalnizca <see cref="MaskCredentials"/> ile cikar).</param>
    private sealed record FfmpegRun(int ExitCode, byte[] Output, string Error, bool TimedOut, bool NotFound);

    /// <summary> Disari verilebilir hata: mesaj SABIT Turkcedir, RTSP adresi icermez. </summary>
    private sealed record CaptureFailure(string Message);

    /// <summary>
    /// FFmpeg'i calistirir. stdout ve stderr ESZAMANLI okunur — biri okunmazsa tamponu dolar ve surec kilitlenir.
    /// Sure asilirsa surec agaciyla oldurulur. Cagiranin iptali (uygulama kapanisi) da sureci oldurur ve istisnayi yukari tasir.
    /// </summary>
    private async Task<FfmpegRun> RunFfmpegAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string exePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, FfmpegRelativePath));
        if (!File.Exists(exePath))
        {
            _logger.LogError("FFmpeg bulunamadi: {Path}", exePath);
            return new FfmpegRun(-1, [], string.Empty, TimedOut: false, NotFound: true);
        }

        // ArgumentList: her arguman ayri gecer, shell/tirnak kacisi yok (parolada bosluk ya da tirnak olsa bile).
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            _logger.LogError(exception, "FFmpeg baslatilamadi: {Path}", exePath);
            return new FfmpegRun(-1, [], string.Empty, TimedOut: false, NotFound: true);
        }

        var outputTask = ReadAllBytesAsync(process.StandardOutput.BaseStream);
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            timedOut = !cancellationToken.IsCancellationRequested;
        }

        // Oldurulen surecin akislari kapanir; okumalar tamamlanir. Beklenmeleri gozlenmemis istisna birakmamak icin de sart.
        byte[] output = await outputTask;
        string error = await errorTask;
        await process.WaitForExitAsync(CancellationToken.None);

        cancellationToken.ThrowIfCancellationRequested();

        if (error.Length > MaxErrorLength)
            error = error[^MaxErrorLength..];

        return new FfmpegRun(timedOut ? -1 : process.ExitCode, output, error, timedOut, NotFound: false);
    }

    /// <summary>
    /// Basarisiz calistirmayi SABIT bir Turkce mesaja cevirir; basariliysa <c>null</c>. Ham stderr yalnizca maskelenip loglanir:
    /// FFmpeg hata satirlarinda kaynak adresini (icinde okuma bileti) tekrarlar ve bu mesaj <c>CameraCapture.FailureReason</c>'a yazilip istemciye gider.
    /// <para/>
    /// FFmpeg kamerayi GORMEZ, MediaMTX'i gorur: kameranin parolasi/markasi yanlis ya da kamera kapali oldugunda MediaMTX yalnizca
    /// "kaynak hazir degil" der. Bu yuzden kamera tarafi hatalari tek, genel bir mesaja duser; ayrinti MediaMTX'in logundadir.
    /// </summary>
    /// <param name="timeoutMessage">Sert sinir asildiginda donecek mesaj (islemin ne oldugunu en iyi cagiran bilir).</param>
    /// <param name="genericMessage">Taninmayan hatada donecek mesaj; varsayilani kamera tarafini isaret eder (yerel kirpma gibi kamerasiz adimlar kendi mesajini verir).</param>
    private CaptureFailure? DescribeFailure(FfmpegRun run, Camera camera, string operation, string timeoutMessage, string genericMessage = CameraUnavailableMessage)
    {
        if (run.NotFound)
            return new CaptureFailure("FFmpeg bulunamadı; sunucuda MediaTools klasörü eksik olabilir.");

        if (!run.TimedOut && run.ExitCode == 0)
            return null;

        _logger.LogWarning(
            "{Operation} alinamadi. Kamera {CameraId} ({IpAddress}), cikis kodu {ExitCode}, zaman asimi {TimedOut}: {Error}",
            operation, camera.Id, camera.IpAddress, run.ExitCode, run.TimedOut, MaskCredentials(run.Error));

        if (run.TimedOut)
            return new CaptureFailure(timeoutMessage);

        string error = run.Error;

        // Tam ifadeler aranir: yalin "401"/"404", hata satirindaki adreste (?timeout=4010000 gibi) de gecebilir.
        // 401 = MediaMTX'in auth kancasi BIZIM bileti reddetti (kameranin parolasi degil — o MediaMTX ile kamera arasinda kalir).
        if (Contains(error, "401 Unauthorized") || Contains(error, "authorization failed"))
            return new CaptureFailure("Medya geçidi çekim biletini reddetti.");

        // "Connection to tcp://127.0.0.1:8554?timeout=... failed" — MediaMTX'in kendisine baglanilamadi.
        if (Contains(error, "Connection to tcp://") || Contains(error, "Connection refused"))
            return new CaptureFailure("Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");

        // Geri kalan her sey (varsayilan olarak) kamera tarafidir: MediaMTX kaynagi (kamerayi) hazir edemedi.
        return new CaptureFailure(genericMessage);
    }

    private const string CameraUnavailableMessage = "Kamera görüntü vermiyor: kameraya ulaşılamadı ya da kimlik bilgileri / marka yanlış olabilir.";

    /// <summary> <c>rtsp://kullanici:parola@</c> → <c>rtsp://***@</c>. Kimlik bilgisi percent-encode edildigi icin <c>/</c> ve <c>@</c> icermez. </summary>
    private static string MaskCredentials(string text) => CredentialPattern().Replace(text, "rtsp://***@");

    [GeneratedRegex(@"rtsp://[^\s/@]+@", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialPattern();

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Surec zaten cikmis.
        }
    }
}
