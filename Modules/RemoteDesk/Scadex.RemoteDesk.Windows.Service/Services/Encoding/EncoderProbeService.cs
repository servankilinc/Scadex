using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Service.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Service.Services.Monitors;

namespace Scadex.RemoteDesk.Windows.Service.Services.Encoding;

/// <summary> Bir adayın bu makinede, bu monitörde sınama sonucu. </summary>
public sealed record ProbeResult(EncoderCandidate Candidate, bool Ok, string? Reason, TimeSpan Duration);

/// <summary>
/// Bir monitörün sınaması: adaylar öncelik sırasıyla. Seçilen = PC tercihi (<paramref name="Preferred"/>) bu monitörde çalışıyorsa o,
/// değilse ilk başarılı aday (otomatik seçim).
/// </summary>
public sealed record MonitorProbe(MonitorInfo Monitor, IReadOnlyList<ProbeResult> Results, DateTime ProbedAt, EncoderKind? Preferred = null)
{
    public ProbeResult? Selected =>
        (Preferred is { } kind ? Results.FirstOrDefault(r => r.Ok && r.Candidate.Kind == kind) : null)
        ?? Results.FirstOrDefault(r => r.Ok);

    /// <summary> Seçim PC tercihinden geldi (otomatik değil). </summary>
    public bool IsPreferenceApplied => Preferred is { } kind && Selected?.Candidate.Kind == kind;

    /// <summary> Tercih var ama bu monitörde çalışmadı (ya da hiç sınanmadı) — otomatik seçime düşüldü. </summary>
    public bool PreferenceUnavailable => Preferred is not null && !IsPreferenceApplied;
}

public interface IEncoderProbeService
{
    /// <summary>
    /// PC başına kodlayıcı tercihi — BELLEKTE, uygulama kapanınca otomatiğe döner (kullanıcı kararı 2026-09-30). <c>null</c> = otomatik.
    /// Tercih edilen aday bir monitörde çalışmıyorsa o monitör otomatik seçimi kullanır.
    /// </summary>
    EncoderKind? Preferred { get; }

    void SetPreferred(EncoderKind? kind);

    /// <summary> Tercih değişti — iş parçacığı havuzundan ya da çağıranın iş parçacığından gelir. </summary>
    event Action? PreferenceChanged;

    /// <summary> Bir monitörün sınaması bitti (elle ya da ilk yayında) — iş parçacığı havuzundan gelir. </summary>
    event Action? Probed;

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
public sealed class EncoderProbeService(IFfmpegLocator ffmpeg, IMonitorService monitors, ChildProcessJob job, Desktop.InputDesktopWatcher desktops,
    ILogger<EncoderProbeService> logger)
    : IEncoderProbeService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private readonly Dictionary<int, MonitorProbe> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);   // sınamalar aynı anda koşmaz: ekran kartını paylaşırlar
    private EncoderKind? _preferred;

    public EncoderKind? Preferred { get { lock (_cache) return _preferred; } }

    public event Action? PreferenceChanged;
    public event Action? Probed;

    public void SetPreferred(EncoderKind? kind)
    {
        lock (_cache)
        {
            if (_preferred == kind) return;
            _preferred = kind;
        }
        logger.LogInformation("Kodlayıcı tercihi: {Preferred}", kind?.ToString() ?? "otomatik");
        PreferenceChanged?.Invoke();
    }

    /// <summary> Önbellekteki sınama o anki tercihle döner: tercih değişince önbelleği güncellemek gerekmez. </summary>
    public MonitorProbe? GetCached(int monitorIndex)
    {
        lock (_cache) return _cache.GetValueOrDefault(monitorIndex) is { } probe ? probe with { Preferred = _preferred } : null;
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
            MonitorProbe current;
            lock (_cache)
            {
                _cache[monitor.Index] = probe;
                current = probe with { Preferred = _preferred };
            }
            Probed?.Invoke();
            return current;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProbeResult> RunAsync(MonitorInfo monitor, EncoderCandidate candidate, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        // Sınama da o an aktif masaüstünde yapılır (UAC / kilit ekranı açıkken Winlogon) — yayınla aynı başlatıcı.
        using var desktopProcess = DesktopProcess.Start(ffmpeg.Info.Path, FfmpegCommand.Probe(monitor, candidate), desktops.Current, job);
        var proc = desktopProcess.Process;
        Task<string> stderr = desktopProcess.StandardError.ReadToEndAsync(cancellationToken);
        _ = desktopProcess.StandardOutput.ReadToEndAsync(cancellationToken);

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
