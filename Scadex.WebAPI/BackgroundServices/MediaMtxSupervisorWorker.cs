using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Scadex.Business.Settings;

namespace Scadex.WebAPI.BackgroundServices;

/// <summary>
/// MediaMTX'i baslatir veya kapalıysa yeniden başlatır. Yapilandirma exe ile ayni klasordeki <c>mediamtx.yml</c>'dir,
/// Scadex portlar yml'de, <c>appsettings &gt; MediaGateway</c> ile senkron olmalı.
/// </summary>
public class MediaMtxSupervisorWorker : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRestartDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MissingFileRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary> Bundan kısa yaşayan sureç "açılamadı" sayılır (port dolu, yml hatası): bekleme katlanarak uzar, log sürekli yazmaz. </summary>
    private static readonly TimeSpan HealthyRunTime = TimeSpan.FromSeconds(10);

    private readonly string _exePath;
    private readonly string _configPath;
    private readonly ILogger<MediaMtxSupervisorWorker> _logger;

    public MediaMtxSupervisorWorker(IWebHostEnvironment environment, IOptions<MediaGatewaySettings> settings, ILogger<MediaMtxSupervisorWorker> logger)
    {
        // Path.Combine ikinci arguman mutlaksa onu dondurur: ayarda mutlak yol da verilebilir.
        _exePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.Value.MediaMtxPath));
        _configPath = Path.Combine(Path.GetDirectoryName(_exePath)!, "mediamtx.yml");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = RestartDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var process = FindRunning() ?? Start();

            if (process is null)
            {
                if (!await DelayAsync(MissingFileRetryDelay, stoppingToken)) break;
                continue;
            }

            var watchedSince = DateTime.UtcNow;

            try
            {
                await process.WaitForExitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            bool exitedQuickly = DateTime.UtcNow - watchedSince < HealthyRunTime;
            if (!exitedQuickly)
                delay = RestartDelay;

            _logger.LogWarning(
                "MediaMTX kapandi (PID {ProcessId}, cikis kodu {ExitCode}); {Delay} sn sonra yeniden baslatiliyor.{Hint}",
                process.Id, TryGetExitCode(process), delay.TotalSeconds,
                exitedQuickly ? " Açılır açılmaz kapandı: port başka bir sureçte olabilir ya da mediamtx.yml hatali." : string.Empty);

            if (!await DelayAsync(delay, stoppingToken)) break;

            if (exitedQuickly)
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRestartDelay.Ticks));
        }
    }

    /// <summary> Ayardaki exe yolundan calisan MediaMTX. Baska yoldan calisan bir MediaMTX bizim degildir, dokunulmaz. </summary>
    private Process? FindRunning()
    {
        Process? found = null;

        foreach (var candidate in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_exePath)))
        {
            if (found is null && IsSameExecutable(candidate))
            {
                found = candidate;
                continue;
            }

            candidate.Dispose();
        }

        if (found is not null)
            _logger.LogInformation("Calisan MediaMTX sahiplenildi (PID {ProcessId}): {Path}", found.Id, _exePath);

        return found;
    }

    private bool IsSameExecutable(Process candidate)
    {
        try
        {
            return string.Equals(candidate.MainModule?.FileName, _exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // Erisilemeyen (baska kullanicinin) ya da bu arada kapanmis surec: bizim degil.
            return false;
        }
    }

    private Process? Start()
    {
        if (!File.Exists(_exePath))
        {
            _logger.LogError("MediaMTX bulunamadi: {Path}. MediaGateway:MediaMtxPath ayarini kontrol edin.", _exePath);
            return null;
        }

        // yml'siz MediaMTX varsayilanlarla acilir: kimlik dogrulama kapali, Control API kapali. Asla boyle baslatilmaz.
        if (!File.Exists(_configPath))
        {
            _logger.LogError("mediamtx.yml bulunamadi: {Path}. MediaMTX baslatilmadi.", _configPath);
            return null;
        }

        var startInfo = new ProcessStartInfo(_exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_exePath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(_configPath);

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => LogMediaMtxLine(e.Data);
        process.ErrorDataReceived += (_, e) => LogMediaMtxLine(e.Data);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            _logger.LogError(exception, "MediaMTX baslatilamadi: {Path}", _exePath);
            process.Dispose();
            return null;
        }

        // Cikti okunmazsa boru tamponu dolar ve MediaMTX yazarken kilitlenir.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _logger.LogInformation("MediaMTX baslatildi (PID {ProcessId}): {Path}", process.Id, _exePath);
        return process;
    }

    /// <summary> MediaMTX satirlari "2026/09/27 16:56:44 INF ..." bicimindedir; seviye buradan esitlenir. </summary>
    private void LogMediaMtxLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var level = line.Contains(" ERR ", StringComparison.Ordinal) ? LogLevel.Error
            : line.Contains(" WAR ", StringComparison.Ordinal) ? LogLevel.Warning
            : line.Contains(" DEB ", StringComparison.Ordinal) ? LogLevel.Debug
            : LogLevel.Information;

        _logger.Log(level, "[MediaMTX] {Line}", line);
    }

    private static string TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode.ToString();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Sahiplenilen (bizim baslatmadigimiz) surecin cikis kodu her zaman okunamaz.
            return "bilinmiyor";
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
