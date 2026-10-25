using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Agent;

/// <summary>
/// Agent ↔ WPF arayüz named pipe'ı (<see cref="AgentPipeContract"/>). Güvenlik sınırı burasıdır — Agent SYSTEM, arayüz normal kullanıcı:
/// <list type="bullet">
/// <item>ACL: SYSTEM tam, <b>bu oturumun logon SID'i</b> okuma/yazma; başka hiç kimse. Başka oturumdaki aynı kullanıcı da açamaz.</item>
/// <item>Her bağlantıda istemcinin oturumu Agent'ınkiyle karşılaştırılır (<c>GetNamedPipeClientSessionId</c>).</item>
/// <item>Ad önceden başka bir süreçce alınmışsa (<c>FirstPipeInstance</c>) Agent pipe'ı açmaz ve bunu hata olarak loglar — sahte sunucuya
/// karşı arayüz de sunucunun yolunu doğrular.</item>
/// <item>Yalnızca <c>subscribe</c> + üç komut kabul edilir; bilinmeyen tip, bozuk JSON ya da 64 KB'yi aşan satır bağlantıyı kapatır.</item>
/// </list>
/// </summary>
public sealed class AgentPipeServer : BackgroundService
{
    private const int MaxInstances = 4;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    private readonly AgentStateService _state;
    private readonly SessionContext _session;
    private readonly ILogger<AgentPipeServer> _logger;

    public AgentPipeServer(AgentStateService state, SessionContext session, ILogger<AgentPipeServer> logger)
    {
        _state = state;
        _session = session;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string name = AgentPipeContract.PipeName(_session.SessionId);
        PipeSecurity security;
        try { security = BuildSecurity(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Arayüz pipe'ının erişim listesi kurulamadı; arayüz bağlanamayacak");
            return;
        }

        bool first = true;
        int failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                var options = PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None);
                pipe = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, MaxInstances, PipeTransmissionMode.Byte, options, 0, 0, security);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // İlk örnekte: ad başka süreçte (sahte sunucu ya da eski Agent). Sonrakilerde: örnek sınırı dolu.
                var delay = Backoff[Math.Min(failures++, Backoff.Length - 1)];
                if (first) _logger.LogError(ex, "Arayüz pipe'ı açılamadı ({Pipe}) — ad başka bir süreçte olabilir; {Delay} sn sonra yeniden denenecek", name, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                continue;
            }

            if (first) _logger.LogInformation("Arayüz pipe'ı açıldı: {Pipe}", name);
            first = false;
            failures = 0;

            try { await pipe.WaitForConnectionAsync(stoppingToken); }
            catch (OperationCanceledException) { await pipe.DisposeAsync(); break; }

            _ = Task.Run(() => ServeAsync(pipe, stoppingToken), stoppingToken);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        await using var connection = pipe;
        if (!Win32.GetNamedPipeClientSessionId(pipe.SafePipeHandle, out uint clientSession) || clientSession != _session.SessionId)
        {
            _logger.LogWarning("Arayüz pipe'ına başka oturumdan bağlantı reddedildi (oturum {ClientSession})", clientSession);
            return;
        }

        var writeGate = new SemaphoreSlim(1, 1);
        bool subscribed = false;
        Action? onChanged = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested && pipe.IsConnected)
            {
                string? line = await ReadLineAsync(pipe, stoppingToken);
                if (line is null) break;

                AgentMessage? message;
                try { message = JsonSerializer.Deserialize<AgentMessage>(line, Json); }
                catch (JsonException) { _logger.LogWarning("Arayüz pipe'ı: bozuk mesaj; bağlantı kapatılıyor"); break; }
                if (message is null) break;
                if (message.V != AgentPipeContract.Version)
                {
                    _logger.LogWarning("Arayüz pipe'ı: protokol sürümü uyuşmuyor (arayüz {Client}, Agent {Agent})", message.V, AgentPipeContract.Version);
                    await WriteAsync(pipe, writeGate, new AgentMessage(AgentPipeContract.Version, AgentPipeContract.UnsupportedVersion), stoppingToken);
                    break;
                }

                switch (message.Type)
                {
                    case AgentPipeContract.Subscribe when !subscribed:
                        subscribed = true;
                        _state.AddSubscriber();
                        onChanged = () => _ = SendSnapshotAsync(pipe, writeGate, stoppingToken);
                        _state.Changed += onChanged;
                        await SendSnapshotAsync(pipe, writeGate, stoppingToken);
                        break;
                    case AgentPipeContract.Reconnect when subscribed:
                        _state.Reconnect();
                        break;
                    case AgentPipeContract.ProbeEncoders when subscribed:
                        _ = _state.ProbeAllAsync();
                        break;
                    case AgentPipeContract.SetPreferredEncoder when subscribed:
                        _state.SetPreferred(message.Encoder);
                        break;
                    default:
                        _logger.LogWarning("Arayüz pipe'ı: izin verilmeyen mesaj {Type}; bağlantı kapatılıyor", message.Type);
                        return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
        }
        finally
        {
            if (subscribed)
            {
                _state.Changed -= onChanged;
                _state.RemoveSubscriber();
            }
        }
    }

    private Task SendSnapshotAsync(NamedPipeServerStream pipe, SemaphoreSlim writeGate, CancellationToken cancellationToken) =>
        pipe.IsConnected
            ? WriteAsync(pipe, writeGate, new AgentMessage(AgentPipeContract.Version, AgentPipeContract.Snapshot, _state.Build()), cancellationToken)
            : Task.CompletedTask;

    private static async Task WriteAsync(NamedPipeServerStream pipe, SemaphoreSlim writeGate, AgentMessage message, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await pipe.WriteAsync(payload, cancellationToken);
            await pipe.WriteAsync("\n"u8.ToArray(), cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Arayüz kapandı; okuma döngüsü bağlantıyı bitirir.
        }
        finally
        {
            writeGate.Release();
        }
    }

    /// <summary> Bir satır (<c>\n</c>'e kadar) okur; <see cref="AgentPipeContract.MaxMessageBytes"/>'ı aşan satır bağlantıyı keser. </summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0) return null;
            if (one[0] == (byte)'\n') return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            if (buffer.Length >= AgentPipeContract.MaxMessageBytes) return null;
            buffer.WriteByte(one[0]);
        }
    }

    private PipeSecurity BuildSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        // Sunucunun sahibi (geliştirmede normal kullanıcı) örnek oluşturabilsin; üretimde bu da SYSTEM'dir.
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(_session.LogonSid(), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return security;
    }
}
