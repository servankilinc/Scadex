using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Windows.Services.Agent;

public enum AgentLinkState
{
    Connecting,
    Connected,
    /// <summary> Bu oturumda Agent yok: servis çalışmıyor ya da Agent yeniden başlıyor. </summary>
    AgentMissing,
    /// <summary> Pipe'ın sunucusu beklenen Agent değil (başka süreç adı almış) — bağlanılmadı. </summary>
    Untrusted,
    /// <summary> Arayüz ile Agent farklı sürümlerden — aynı paketten kurulmalı. </summary>
    VersionMismatch,
}

public interface IAgentLink
{
    AgentLinkState State { get; }

    /// <summary> Agent'ın son özeti; bağlı değilken son bilinen değer kalır (gösterimde "bağlı değil" ile birlikte). </summary>
    AgentSnapshot? Snapshot { get; }

    /// <summary> Durum ya da özet değişti — iş parçacığı havuzundan gelir, UI Dispatcher'a taşınmalıdır. </summary>
    event Action? Changed;

    void Reconnect();
    void ProbeEncoders();
    void SetPreferredEncoder(EncoderKind? kind);
}

/// <summary>
/// Agent'ın named pipe'ına bağlanır (<see cref="AgentPipeContract"/>), özetleri alır ve üç zararsız komutu gönderir. Arayüz kapansa da
/// izleme sürer: iş Agent'tadır. Bağlanmadan önce sunucu doğrulanır — pipe adını başka bir süreç almışsa (sahte "izlenmiyor" göstergesi)
/// bağlanılmaz: sunucu aynı oturumda olmalı ve kurulumdaki servis exe'si olmalı (<see cref="ExpectedAgentPath"/>).
/// </summary>
public sealed partial class AgentPipeClient : BackgroundService, IAgentLink
{
    private const string AgentExeName = "Scadex.RemoteDesk.Windows.Service.exe";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    private readonly ILogger<AgentPipeClient> _logger;
    private readonly int _sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
    private readonly Lock _gate = new();
    private NamedPipeClientStream? _pipe;
    private bool _devPathWarned;

    public AgentPipeClient(ILogger<AgentPipeClient> logger) => _logger = logger;

    public AgentLinkState State { get; private set; } = AgentLinkState.Connecting;
    public AgentSnapshot? Snapshot { get; private set; }
    public event Action? Changed;

    /// <summary> Kurulum düzeni: <c>&lt;kök&gt;\Tray\</c> (bu uygulama) ve <c>&lt;kök&gt;\Service\</c> (servis + Agent). </summary>
    private static string ExpectedAgentPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Service", AgentExeName));

    public void Reconnect() => Send(new AgentMessage(AgentPipeContract.Version, AgentPipeContract.Reconnect));
    public void ProbeEncoders() => Send(new AgentMessage(AgentPipeContract.Version, AgentPipeContract.ProbeEncoders));
    public void SetPreferredEncoder(EncoderKind? kind) => Send(new AgentMessage(AgentPipeContract.Version, AgentPipeContract.SetPreferredEncoder, Encoder: kind));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var outcome = await RunOnceAsync(stoppingToken);
            if (outcome == AgentLinkState.Connected) failures = 0;

            TimeSpan wait = outcome is AgentLinkState.Untrusted or AgentLinkState.VersionMismatch
                ? TimeSpan.FromSeconds(30)
                : Backoff[Math.Min(failures++, Backoff.Length - 1)];
            try { await Task.Delay(wait, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary> Tek bağlantı denemesi; bağlantı bitince son durumu döner. </summary>
    private async Task<AgentLinkState> RunOnceAsync(CancellationToken stoppingToken)
    {
        await using var pipe = new NamedPipeClientStream(".", AgentPipeContract.PipeName(_sessionId), PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(TimeSpan.FromSeconds(2), stoppingToken);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            Publish(AgentLinkState.AgentMissing);
            return AgentLinkState.AgentMissing;
        }

        if (VerifyServer(pipe) is { } reason)
        {
            _logger.LogError("Agent pipe'ının sunucusu doğrulanamadı, bağlanılmadı: {Reason}", reason);
            Publish(AgentLinkState.Untrusted);
            return AgentLinkState.Untrusted;
        }

        lock (_gate) _pipe = pipe;
        try
        {
            await WriteAsync(pipe, new AgentMessage(AgentPipeContract.Version, AgentPipeContract.Subscribe), stoppingToken);
            _logger.LogInformation("Agent'a bağlandı");

            while (!stoppingToken.IsCancellationRequested)
            {
                string? line = await ReadLineAsync(pipe, stoppingToken);
                if (line is null) break;

                var message = JsonSerializer.Deserialize<AgentMessage>(line, Json);
                if (message?.Type == AgentPipeContract.UnsupportedVersion)
                {
                    _logger.LogWarning("Agent farklı protokol sürümünde (Agent {Agent}, arayüz {Ui})", message.V, AgentPipeContract.Version);
                    Publish(AgentLinkState.VersionMismatch);
                    return AgentLinkState.VersionMismatch;
                }
                if (message is { Type: AgentPipeContract.Snapshot, Snapshot: { } snapshot })
                {
                    Snapshot = snapshot;
                    Publish(AgentLinkState.Connected);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or OperationCanceledException or ObjectDisposedException)
        {
            if (ex is JsonException) _logger.LogWarning(ex, "Agent'tan bozuk mesaj");
        }
        finally
        {
            lock (_gate) _pipe = null;
        }

        bool wasConnected = State == AgentLinkState.Connected;
        if (wasConnected) _logger.LogInformation("Agent bağlantısı kapandı");
        Publish(AgentLinkState.AgentMissing);
        return wasConnected ? AgentLinkState.Connected : AgentLinkState.AgentMissing;
    }

    /// <summary> Sunucu aynı oturumda mı ve kurulumdaki Agent exe'si mi. <c>null</c> = güvenilir. </summary>
    private string? VerifyServer(NamedPipeClientStream pipe)
    {
        if (!GetNamedPipeServerSessionId(pipe.SafePipeHandle, out uint serverSession) || serverSession != _sessionId)
            return $"sunucu başka oturumda ({serverSession})";
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint serverPid))
            return "sunucu süreci okunamadı";

        string? image = ProcessImagePath(serverPid);
        if (image is null)
            return $"sunucu sürecinin yolu okunamadı (süreç {serverPid})";

        string expected = ExpectedAgentPath;
        if (File.Exists(expected))
            return string.Equals(image, expected, StringComparison.OrdinalIgnoreCase) ? null : $"beklenmeyen sunucu: {image}";

        // Geliştirme düzeni (bin klasörleri): kurulum yolu yok, yalnızca exe adı karşılaştırılır.
        if (!_devPathWarned)
        {
            _logger.LogWarning("Kurulum düzeni bulunamadı ({Expected}); Agent yalnızca exe adıyla doğrulanıyor (geliştirme)", expected);
            _devPathWarned = true;
        }
        return string.Equals(Path.GetFileName(image), AgentExeName, StringComparison.OrdinalIgnoreCase) ? null : $"beklenmeyen sunucu: {image}";
    }

    private void Send(AgentMessage message)
    {
        NamedPipeClientStream? pipe;
        lock (_gate) pipe = _pipe;
        if (pipe is null || State != AgentLinkState.Connected) return;
        _ = WriteAsync(pipe, message, CancellationToken.None).ContinueWith(
            t => _logger.LogWarning(t.Exception, "Agent'a komut gönderilemedi"), TaskContinuationOptions.OnlyOnFaulted);
    }

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    private async Task WriteAsync(Stream pipe, AgentMessage message, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await pipe.WriteAsync(payload, cancellationToken);
            await pipe.WriteAsync("\n"u8.ToArray(), cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        finally { _writeGate.Release(); }
    }

    /// <summary> Bir satır; çok uzun satır (sınır 4 × 64 KB — özet komuttan büyüktür) bağlantıyı keser. </summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) return null;
            if (chunk[0] == (byte)'\n') return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            if (buffer.Length >= 4 * AgentPipeContract.MaxMessageBytes) return null;
            buffer.WriteByte(chunk[0]);
        }
    }

    private void Publish(AgentLinkState state)
    {
        State = state;
        Changed?.Invoke();
    }

    private static unsafe string? ProcessImagePath(uint processId)
    {
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            char* buffer = stackalloc char[1024];
            int size = 1024;
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally { CloseHandle(process); }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerSessionId(SafePipeHandle pipe, out uint sessionId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(IntPtr process, uint flags, char* name, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
