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

    /// <summary> Tüm yayınları durdurur: merkez bağlantısı koptuğunda ve uygulama kapanırken. </summary>
    Task StopAllAsync();

    /// <summary>
    /// Merkezin <c>StartScreenStream</c> komutu (RemoteDesk.md § 6.3): aynı oturum zaten çalışıyorsa hiçbir şey yapmaz; monitörde başka bir yayın
    /// (eski bir oturumun yayını) varsa önce onu durdurur.
    /// </summary>
    Task StartCentralAsync(MonitorInfo monitor, StreamTarget target);

    /// <summary> Merkezin <c>StopScreenStream</c> komutu; bilinmeyen/eski oturum için sessizce başarılıdır. </summary>
    Task StopSessionAsync(Guid sessionId);
}

/// <summary>
/// Monitör başına en fazla bir FFmpeg yayını; yayın YALNIZCA merkezin <c>StartScreenStream</c> komutuyla başlar (yayın bileti adreste,
/// oturum kimliğiyle). Encoder tercihi değişince çalışan yayınlar AYNI hedefle yeniden başlar.
/// </summary>
public sealed class ScreenStreamService : IScreenStreamService
{
    private readonly IEncoderProbeService probes;
    private readonly IFfmpegLocator ffmpeg;
    private readonly ChildProcessJob job;
    private readonly ILogger<ScreenStreamService> logger;

    private readonly Dictionary<int, StreamSession> _sessions = [];
    /// <summary> Tercih değişiminden doğan yeniden başlatmalar sırayla yapılır (art arda tıklamada yarış olmasın). </summary>
    private readonly SemaphoreSlim _restartGate = new(1, 1);

    public ScreenStreamService(IEncoderProbeService probes, IFfmpegLocator ffmpeg, ChildProcessJob job, ILogger<ScreenStreamService> logger)
    {
        this.probes = probes;
        this.ffmpeg = ffmpeg;
        this.job = job;
        this.logger = logger;
        probes.PreferenceChanged += () => _ = ApplyPreferenceAsync();
    }

    public event Action<StreamStatus>? StatusChanged;

    public StreamStatus GetStatus(int monitorIndex)
    {
        lock (_sessions) return _sessions.TryGetValue(monitorIndex, out var s) ? s.Status : new StreamStatus(monitorIndex, StreamState.Idle);
    }

    /// <summary> Encoder sınamadan seçilir: PC tercihi (bu monitörde çalışıyorsa) ya da otomatik seçim. </summary>
    private void Start(MonitorInfo monitor, StreamTarget target)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(monitor.Index, out var existing) && !existing.IsFinished) return;   // idempotent
            var session = NewSession(monitor, target);
            _sessions[monitor.Index] = session;
            session.Run();
        }
    }

    private async Task StopAsync(int monitorIndex)
    {
        StreamSession? session;
        lock (_sessions) _sessions.Remove(monitorIndex, out session);
        if (session is not null) await session.StopAsync();
        StatusChanged?.Invoke(new StreamStatus(monitorIndex, StreamState.Idle) { SessionId = session?.Target.SessionId });
    }

    public async Task StartCentralAsync(MonitorInfo monitor, StreamTarget target)
    {
        StreamSession? running;
        lock (_sessions) _sessions.TryGetValue(monitor.Index, out running);

        if (running is not null && !running.IsFinished && running.Target.SessionId == target.SessionId)
            return;   // aynı oturum: idempotent, yeni FFmpeg açılmaz
        // Monitörde eski bir oturumun yayını varsa önce o durur.
        if (running is not null)
            await StopAsync(monitor.Index);

        Start(monitor, target);
    }

    public async Task StopSessionAsync(Guid sessionId)
    {
        int[] indexes;
        lock (_sessions) indexes = [.. _sessions.Where(s => s.Value.Target.SessionId == sessionId).Select(s => s.Key)];
        await Task.WhenAll(indexes.Select(StopAsync));
    }

    public async Task StopAllAsync()
    {
        int[] indexes;
        lock (_sessions) indexes = [.. _sessions.Keys];
        await Task.WhenAll(indexes.Select(StopAsync));
    }

    private StreamSession NewSession(MonitorInfo monitor, StreamTarget target) =>
        new(monitor, target, probes, ffmpeg, job, logger, status => StatusChanged?.Invoke(status));

    /// <summary> Tercih değişti: kodlayıcısı yeni seçimden farklı olan her çalışan yayın yeniden başlar. </summary>
    private async Task ApplyPreferenceAsync()
    {
        await _restartGate.WaitAsync();
        try
        {
            List<StreamSession> running;
            lock (_sessions) running = [.. _sessions.Values.Where(s => !s.IsFinished)];

            foreach (var session in running)
            {
                // Monitör henüz sınanmadıysa (yayın sınama aşamasında) seçim zaten güncel tercihle yapılacak.
                if (probes.GetCached(session.Monitor.Index)?.Selected?.Candidate is not { } selected || session.Status.Encoder?.Kind == selected.Kind)
                    continue;
                await RestartAsync(session);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kodlayıcı tercihi çalışan yayınlara uygulanamadı");
        }
        finally
        {
            _restartGate.Release();
        }
    }

    /// <summary>
    /// Aynı hedefle (merkez yayınında aynı oturum, yayın adresi, profil) yeni FFmpeg. Arada Idle YAYINLANMAZ: merkeze "durdu" gitseydi
    /// sunucu oturumu kapatırdı. Merkeze Starting → Streaming gider; izleyicinin WHEP bağlantısı kopar ve tarayıcı yeniden bağlanır.
    /// </summary>
    private async Task RestartAsync(StreamSession old)
    {
        await old.StopAsync();
        lock (_sessions)
        {
            // Beklerken durdurulduysa ya da yerine başka yayın geldiyse (merkezin durdurma/yeni oturum komutu) yeniden başlatılmaz.
            if (!_sessions.TryGetValue(old.Monitor.Index, out var current) || !ReferenceEquals(current, old))
                return;

            var fresh = NewSession(old.Monitor, old.Target);
            _sessions[old.Monitor.Index] = fresh;
            fresh.Run();
        }
        logger.LogInformation("Monitör {Monitor}: kodlayıcı tercihi değişti, yayın yeniden başlatıldı", old.Monitor.Index);
    }
}

/// <summary> Tek monitörün yayın döngüsü: seçim → FFmpeg → izleme → (düşür / yeniden dene / kilit bekle). </summary>
internal sealed class StreamSession(
    MonitorInfo monitor, StreamTarget target, IEncoderProbeService probes, IFfmpegLocator ffmpeg, ChildProcessJob job,
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
    /// <summary> Durdurma istendi: süreç kapanınca "yeniden deneniyor" yayınlanmaz, döngü biter. </summary>
    private volatile bool _stopping;

    public MonitorInfo Monitor => monitor;
    public StreamTarget Target => target;
    public StreamStatus Status { get; private set; } = new(monitor.Index, StreamState.Idle) { SessionId = target.SessionId };
    public bool IsFinished => _loop.IsCompleted && Status.State is StreamState.Failed or StreamState.Idle;

    public void Run() => _loop = Task.Run(LoopAsync);

    /// <summary>
    /// SIRA ÖNEMLİ: önce FFmpeg nazikçe durdurulur, SONRA iptal edilir. Önce iptal edilseydi çıktı okuyan döngü hemen kesilir,
    /// <c>using</c> süreç nesnesini süreç hâlâ çalışırken kapatır ve ardından gelen 'q' / Kill kapatılmış nesnede sessizce başarısız
    /// olurdu — FFmpeg sahipsiz kalırdı (2026-09-30'da kodlayıcı tercihi değişiminde yakalandı).
    /// </summary>
    public async Task StopAsync()
    {
        _stopping = true;
        await StopProcessAsync();
        _stop.Cancel();
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
            // 1) Seçim — ilk yayında sınanır (boştayken değil), sonuç önbellekte kalır. PC tercihi bu monitörde çalışıyorsa o,
            //    değilse öncelik sırasındaki ilk çalışan aday (MonitorProbe.Selected).
            MonitorProbe? probe = probes.GetCached(monitor.Index);
            if (probe?.Selected is null)
            {
                Publish(Status with { State = StreamState.Probing, Message = "Encoder'lar sınanıyor…" });
                probe = await probes.ProbeAsync(monitor, ct);
            }
            if (probe.Selected is not { } selected)
            {
                string reason = probe.Results.LastOrDefault()?.Reason ?? "aday yok";
                Publish(Status with { State = StreamState.Failed, Message = $"Hiçbir encoder çalışmadı — {reason}" });
                return;
            }
            EncoderCandidate encoder = selected.Candidate;
            VideoProfile profile = target.Profile ?? encoder.DefaultProfile;
            int attempt = 0, downgrades = 0, restarts = 0;
            Publish(Status with { Encoder = encoder, Profile = profile, Target = target.Describe(), IsForced = probe.IsPreferenceApplied });

            // 2) Yayın döngüsü.
            while (!ct.IsCancellationRequested)
            {
                RunOutcome outcome = await RunOnceAsync(encoder, profile, downgrades, restarts, ct);
                if (ct.IsCancellationRequested || _stopping) break;

                switch (outcome.Kind)
                {
                    case RunOutcomeKind.Slow when Lower(profile, encoder.CanScale) is { } lower:
                        logger.LogWarning("Monitör {Monitor}: kodlayıcı yetişemiyor, profil {From} → {To}", monitor.Index, profile, lower);
                        profile = lower;
                        downgrades++;
                        attempt = 0;
                        continue;
                    case RunOutcomeKind.Unauthorized:
                        // Bilet geçersiz: merkez oturumu kapatmış. Yeniden denemek anlamsız (MediaMTX her seferinde 401 döner).
                        Publish(Status with { State = StreamState.Failed, Message = outcome.Message, Fps = null, Speed = null });
                        return;
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

    private enum RunOutcomeKind { Exited, Slow, ScreenLocked, Unauthorized }
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
            encoder.DisplayName, encoder.Chain, profile, target.Describe());

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

        try
        {
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
        }
        catch (OperationCanceledException)
        {
            // Güvenlik ağı: iptal süreç hâlâ çalışırken geldiyse "using" onu sahipsiz bırakmasın.
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }

        await proc.WaitForExitAsync(CancellationToken.None);
        try { await stderrPump; } catch (OperationCanceledException) { }
        _process = null;
        if (slow) return new RunOutcome(RunOutcomeKind.Slow);

        string stderr;
        lock (stderrLines) stderr = string.Join('\n', stderrLines);
        if (!string.IsNullOrWhiteSpace(stderr)) logger.LogWarning("Monitör {Monitor} FFmpeg çıktı ({Code}): {Stderr}", monitor.Index, proc.ExitCode, FailureExplainer.Mask(stderr));
        if (FailureExplainer.IsUnauthorized(stderr))
            return new RunOutcome(RunOutcomeKind.Unauthorized, FailureExplainer.Unauthorized);
        return FailureExplainer.IsScreenLocked(stderr)
            ? new RunOutcome(RunOutcomeKind.ScreenLocked)
            : new RunOutcome(RunOutcomeKind.Exited, $"FFmpeg kapandı (kod {proc.ExitCode}): {FailureExplainer.Explain(stderr)}");
    }

    private string[] OutputArgs() => ["-rtsp_transport", "tcp", "-f", "rtsp", target.RtspUrl];

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
