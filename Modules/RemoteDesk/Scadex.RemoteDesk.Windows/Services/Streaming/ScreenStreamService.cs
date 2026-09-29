using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;

namespace Scadex.RemoteDesk.Windows.Services.Streaming;

public interface IScreenStreamService
{
    /// <summary> Durum değişince yayınlanır — İŞ PARÇACIĞI HAVUZUNDAN; arayüz Dispatcher'a taşımalı. </summary>
    event Action<StreamStatus>? StatusChanged;

    StreamStatus GetStatus(int monitorIndex);
    /// <summary> <paramref name="forced"/> verilirse otomatik seçim atlanır — sahada adayları karşılaştırmak için. </summary>
    void Start(MonitorInfo monitor, StreamTarget target, EncoderCandidate? forced = null);
    Task StopAsync(int monitorIndex);
    Task StopAllAsync();
}

/// <summary>
/// Monitör başına en fazla bir FFmpeg yayını. Şimdilik sahadaki "test yayını" için: merkez
/// (PcHub) gelince aynı servis <c>StartScreenStream</c> komutuyla, yayın bileti ve sunucunun verdiği profille çağrılır.
/// </summary>
public sealed class ScreenStreamService(IEncoderProbeService probes, IFfmpegLocator ffmpeg, ChildProcessJob job, ILogger<ScreenStreamService> logger)
    : IScreenStreamService
{
    private readonly Dictionary<int, StreamSession> _sessions = [];

    public event Action<StreamStatus>? StatusChanged;

    public StreamStatus GetStatus(int monitorIndex)
    {
        lock (_sessions) return _sessions.TryGetValue(monitorIndex, out var s) ? s.Status : new StreamStatus(monitorIndex, StreamState.Idle);
    }

    public void Start(MonitorInfo monitor, StreamTarget target, EncoderCandidate? forced = null)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(monitor.Index, out var existing) && !existing.IsFinished) return;   // idempotent
            var session = new StreamSession(monitor, target, forced, probes, ffmpeg, job, logger, status => StatusChanged?.Invoke(status));
            _sessions[monitor.Index] = session;
            session.Run();
        }
    }

    public async Task StopAsync(int monitorIndex)
    {
        StreamSession? session;
        lock (_sessions) _sessions.Remove(monitorIndex, out session);
        if (session is not null) await session.StopAsync();
        StatusChanged?.Invoke(new StreamStatus(monitorIndex, StreamState.Idle));
    }

    public async Task StopAllAsync()
    {
        int[] indexes;
        lock (_sessions) indexes = [.. _sessions.Keys];
        await Task.WhenAll(indexes.Select(StopAsync));
    }
}

/// <summary> Tek monitörün yayın döngüsü: seçim → FFmpeg → izleme → (düşür / yeniden dene / kilit bekle). </summary>
internal sealed class StreamSession(
    MonitorInfo monitor, StreamTarget target, EncoderCandidate? forced, IEncoderProbeService probes, IFfmpegLocator ffmpeg, ChildProcessJob job,
    ILogger logger, Action<StreamStatus> publish)
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];
    private static readonly TimeSpan LockedRetryDelay = TimeSpan.FromSeconds(5);
    /// <summary> Geri kalma bekçisi: hız bu sürenin tamamında 0,9'un altındaysa profil bir basamak düşer (§ 7.5). </summary>
    private static readonly TimeSpan SlowWindow = TimeSpan.FromSeconds(10);
    private const double SlowSpeed = 0.9;

    private readonly CancellationTokenSource _stop = new();
    private Task _loop = Task.CompletedTask;
    private Process? _process;

    public StreamStatus Status { get; private set; } = new(monitor.Index, StreamState.Idle);
    public bool IsFinished => _loop.IsCompleted && Status.State is StreamState.Failed or StreamState.Idle;

    public void Run() => _loop = Task.Run(LoopAsync);

    public async Task StopAsync()
    {
        _stop.Cancel();
        await StopProcessAsync();
        try { await _loop; } catch (OperationCanceledException) { }
    }

    private void Publish(StreamStatus status)
    {
        Status = status;
        publish(status);
    }

    private async Task LoopAsync()
    {
        CancellationToken ct = _stop.Token;
        try
        {
            // 1) Seçim — ilk yayında sınanır (boştayken değil), sonuç önbellekte kalır. Elle seçilen aday sınanmaz:
            //    sahada kullanıcı bilerek dener; açılmazsa hata nedeni zaten durumda görünür.
            EncoderCandidate encoder;
            if (forced is not null)
            {
                encoder = forced;
            }
            else
            {
                MonitorProbe? probe = probes.GetCached(monitor.Index);
                if (probe?.Selected is null)
                {
                    Publish(Status with { State = StreamState.Probing, Message = "Kodlayıcılar sınanıyor…" });
                    probe = await probes.ProbeAsync(monitor, ct);
                }
                if (probe.Selected is not { } selected)
                {
                    string reason = probe.Results.LastOrDefault()?.Reason ?? "aday yok";
                    Publish(Status with { State = StreamState.Failed, Message = $"Hiçbir kodlayıcı çalışmadı — {reason}" });
                    return;
                }
                encoder = selected.Candidate;
            }
            VideoProfile profile = encoder.DefaultProfile;
            int attempt = 0, downgrades = 0, restarts = 0;
            Publish(Status with { Encoder = encoder, Profile = profile, Target = target.Describe(monitor.Index), IsForced = forced is not null });

            // 2) Yayın döngüsü.
            while (!ct.IsCancellationRequested)
            {
                RunOutcome outcome = await RunOnceAsync(encoder, profile, downgrades, restarts, ct);
                if (ct.IsCancellationRequested) break;

                switch (outcome.Kind)
                {
                    case RunOutcomeKind.Slow when Lower(profile, encoder.CanScale) is { } lower:
                        logger.LogWarning("Monitör {Monitor}: kodlayıcı yetişemiyor, profil {From} → {To}", monitor.Index, profile, lower);
                        profile = lower;
                        downgrades++;
                        attempt = 0;
                        continue;
                    case RunOutcomeKind.ScreenLocked:
                        Publish(Status with { State = StreamState.ScreenLocked, Message = FailureExplainer.ScreenLocked, Fps = null, Speed = null });
                        await Task.Delay(LockedRetryDelay, ct);
                        continue;
                    default:
                        restarts++;
                        TimeSpan delay = RetryDelays[Math.Min(attempt++, RetryDelays.Length - 1)];
                        Publish(Status with { State = StreamState.Retrying, Restarts = restarts, Message = $"{outcome.Message} — {delay.TotalSeconds:0} sn sonra yeniden denenecek" });
                        await Task.Delay(delay, ct);
                        continue;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Monitör {Monitor} yayın döngüsü düştü", monitor.Index);
            Publish(Status with { State = StreamState.Failed, Message = ex.Message });
        }
    }

    private enum RunOutcomeKind { Exited, Slow, ScreenLocked }
    private sealed record RunOutcome(RunOutcomeKind Kind, string? Message = null);

    private async Task<RunOutcome> RunOnceAsync(EncoderCandidate encoder, VideoProfile profile, int downgrades, int restarts, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ffmpeg.Info.Path)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string arg in FfmpegCommand.Capture(monitor, encoder, profile)) psi.ArgumentList.Add(arg);
        foreach (string arg in (string[])["-progress", "pipe:1", "-stats_period", "1"]) psi.ArgumentList.Add(arg);
        foreach (string arg in OutputArgs()) psi.ArgumentList.Add(arg);

        Publish(Status with
        {
            State = StreamState.Starting, Encoder = encoder, Profile = profile, Downgrades = downgrades, Restarts = restarts,
            StartedAt = DateTime.Now, Fps = null, Speed = null, Bitrate = null, CpuPercent = null, Message = null,
        });
        logger.LogInformation("Monitör {Monitor} yayın: {Encoder} ({Chain}), {Profile} → {Target}", monitor.Index,
            encoder.DisplayName, encoder.Chain, profile, target.Describe(monitor.Index));

        using var proc = Process.Start(psi)!;
        _process = proc;
        job.Add(proc);
        try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* süreç çoktan çıkmış olabilir */ }

        var stderrLines = new List<string>();
        Task stderrPump = Task.Run(async () =>
        {
            while (await proc.StandardError.ReadLineAsync(ct) is { } line)
                lock (stderrLines) { stderrLines.Add(line); if (stderrLines.Count > 40) stderrLines.RemoveAt(0); }
        }, ct);

        var block = new Dictionary<string, string>();
        DateTime? slowSince = null;
        TimeSpan lastCpu = TimeSpan.Zero;
        var cpuClock = Stopwatch.StartNew();
        bool slow = false;

        while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
        {
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            block[line[..eq]] = line[(eq + 1)..].Trim();
            if (line[..eq] != "progress") continue;

            double? speed = double.TryParse(block.GetValueOrDefault("speed")?.TrimEnd('x'), System.Globalization.CultureInfo.InvariantCulture, out var sp) ? sp : null;
            double? fps = double.TryParse(block.GetValueOrDefault("fps"), System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : null;
            long frames = long.TryParse(block.GetValueOrDefault("frame"), out var fr) ? fr : 0;

            // CPU: süreç zamanı / duvar saati / çekirdek sayısı — yalnızca yayın sürerken ölçülür.
            double? cpu = null; long? memMb = null;
            try
            {
                proc.Refresh();
                TimeSpan total = proc.TotalProcessorTime;
                double elapsedMs = cpuClock.Elapsed.TotalMilliseconds;
                if (elapsedMs > 0) cpu = Math.Round((total - lastCpu).TotalMilliseconds / elapsedMs / Environment.ProcessorCount * 100, 1);
                lastCpu = total; cpuClock.Restart();
                memMb = proc.WorkingSet64 / (1024 * 1024);
            }
            catch (InvalidOperationException) { /* süreç kapandı */ }

            Publish(Status with
            {
                State = frames > 0 ? StreamState.Streaming : StreamState.Starting,
                Fps = fps, Speed = speed, Bitrate = block.GetValueOrDefault("bitrate"), CpuPercent = cpu, MemoryMb = memMb,
            });
            block.Clear();

            // Geri kalma bekçisi: passthrough sınırsız birikmeyi önler; bekçi düşük fps'li takılmayı önler.
            if (frames > 0 && speed is < SlowSpeed)
            {
                slowSince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - slowSince >= SlowWindow && Lower(profile, encoder.CanScale) is not null)
                {
                    slow = true;
                    await StopProcessAsync();
                    break;
                }
            }
            else slowSince = null;
        }

        await proc.WaitForExitAsync(CancellationToken.None);
        try { await stderrPump; } catch (OperationCanceledException) { }
        _process = null;
        if (slow) return new RunOutcome(RunOutcomeKind.Slow);

        string stderr;
        lock (stderrLines) stderr = string.Join('\n', stderrLines);
        if (!string.IsNullOrWhiteSpace(stderr)) logger.LogWarning("Monitör {Monitor} FFmpeg çıktı ({Code}): {Stderr}", monitor.Index, proc.ExitCode, FailureExplainer.Mask(stderr));
        return FailureExplainer.IsScreenLocked(stderr)
            ? new RunOutcome(RunOutcomeKind.ScreenLocked)
            : new RunOutcome(RunOutcomeKind.Exited, $"FFmpeg kapandı (kod {proc.ExitCode}): {FailureExplainer.Explain(stderr)}");
    }

    private string[] OutputArgs() => target.Kind == StreamTargetKind.EncodeOnly
        ? ["-f", "null", "-"]
        : ["-rtsp_transport", "tcp", "-f", "rtsp", target.ResolveUrl(monitor.Index)];

    /// <summary> Nazik durdurma: stdin'e 'q', 3 sn içinde çıkmazsa öldür (RemoteDesk.md § 7.5). </summary>
    private async Task StopProcessAsync()
    {
        Process? proc = _process;
        if (proc is null) return;
        try
        {
            if (proc.HasExited) return;
            await proc.StandardInput.WriteAsync('q');
            await proc.StandardInput.FlushAsync();
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await proc.WaitForExitAsync(wait.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
        {
            try { proc.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    /// <summary> Profil merdiveni: önce fps, sonra (ölçekleyebilen zincirde) genişlik; en alt basamakta null. </summary>
    internal static VideoProfile? Lower(VideoProfile p, bool canScale) => p switch
    {
        { Fps: > 20 } => p with { Fps = 20 },
        { Fps: > 15 } => p with { Fps = 15 },
        { MaxWidth: > 1600 } when canScale => p with { MaxWidth = 1600 },
        { MaxWidth: > 1280 } when canScale => p with { MaxWidth = 1280 },
        { Fps: > 10 } => p with { Fps = 10 },
        _ => null,
    };
}
