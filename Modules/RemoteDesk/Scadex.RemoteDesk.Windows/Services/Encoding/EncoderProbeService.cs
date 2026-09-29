using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Services.Monitors;

namespace Scadex.RemoteDesk.Windows.Services.Encoding;

/// <summary> Bir adayın bu makinede, bu monitörde sınama sonucu. </summary>
public sealed record ProbeResult(EncoderCandidate Candidate, bool Ok, string? Reason, TimeSpan Duration);

/// <summary> Bir monitörün sınaması: adaylar öncelik sırasıyla ve seçilen (ilk başarılı) aday. </summary>
public sealed record MonitorProbe(MonitorInfo Monitor, IReadOnlyList<ProbeResult> Results, DateTime ProbedAt)
{
    public ProbeResult? Selected => Results.FirstOrDefault(r => r.Ok);
}

public interface IEncoderProbeService
{
    /// <summary> Monitörün son sınaması (yoksa null). </summary>
    MonitorProbe? GetCached(int monitorIndex);

    /// <summary>
    /// Monitörün tüm adaylarını sırayla sınar ve önbelleğe koyar. Hepsi sınanır — seçim ilk başarılıdır ama sahada
    /// neyin neden çalışmadığını görmek için tablo eksiksiz olmalı.
    /// </summary>
    Task<MonitorProbe> ProbeAsync(MonitorInfo monitor, CancellationToken cancellationToken);
}

/// <summary>
/// Kodlayıcı seçimi üretici adına göre değil gerçek denemeyle yapılır: kart var ama sürücü
/// eski, kart var ama kodeği sunmuyor gibi durumlar ancak böyle görünür. Sınama boştayken değil istenince ya da
/// ilk yayında yapılır; sonuç monitör başına bellekte tutulur.
/// </summary>
public sealed class EncoderProbeService(IFfmpegLocator ffmpeg, IMonitorService monitors, ChildProcessJob job, ILogger<EncoderProbeService> logger)
    : IEncoderProbeService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private readonly Dictionary<int, MonitorProbe> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);   // sınamalar aynı anda koşmaz: ekran kartını paylaşırlar

    public MonitorProbe? GetCached(int monitorIndex)
    {
        lock (_cache) return _cache.GetValueOrDefault(monitorIndex);
    }

    public async Task<MonitorProbe> ProbeAsync(MonitorInfo monitor, CancellationToken cancellationToken)
    {
        if (!ffmpeg.Info.Exists) throw new InvalidOperationException($"FFmpeg bulunamadı: {ffmpeg.Info.Path}");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var vendors = monitors.GetMonitors().Select(m => m.GpuVendor);
            var results = new List<ProbeResult>();
            foreach (EncoderCandidate candidate in EncoderCatalog.OrderFor(monitor, vendors))
            {
                ProbeResult result = await RunAsync(monitor, candidate, cancellationToken);
                results.Add(result);
                logger.LogInformation("Sınama monitör {Monitor} {Encoder} ({Chain}): {Result}", monitor.Index,
                    candidate.DisplayName, candidate.Chain, result.Ok ? "çalışıyor" : result.Reason);
                // Ekran kilitliyse diğer adayları denemek anlamsız: hepsi aynı nedenle düşer, tablo yanıltır.
                if (result.Reason == FailureExplainer.ScreenLocked) break;
            }

            var probe = new MonitorProbe(monitor, results, DateTime.Now);
            lock (_cache) _cache[monitor.Index] = probe;
            return probe;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProbeResult> RunAsync(MonitorInfo monitor, EncoderCandidate candidate, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(ffmpeg.Info.Path)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
        };
        foreach (string arg in FfmpegCommand.Probe(monitor, candidate)) psi.ArgumentList.Add(arg);

        var watch = Stopwatch.StartNew();
        using var proc = Process.Start(psi)!;
        job.Add(proc);
        Task<string> stderr = proc.StandardError.ReadToEndAsync(cancellationToken);
        _ = proc.StandardOutput.ReadToEndAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            await proc.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            proc.Kill(entireProcessTree: true);
            return new ProbeResult(candidate, false, $"Zaman aşımı ({ProbeTimeout.TotalSeconds:0} sn içinde kare üretmedi)", watch.Elapsed);
        }

        return proc.ExitCode == 0
            ? new ProbeResult(candidate, true, null, watch.Elapsed)
            : new ProbeResult(candidate, false, FailureExplainer.Explain(await stderr), watch.Elapsed);
    }
}
