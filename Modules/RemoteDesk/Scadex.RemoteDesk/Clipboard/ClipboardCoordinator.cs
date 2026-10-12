using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Hubs;
using Scadex.RemoteDesk.Model.Dtos.Clipboard;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Streaming;

namespace Scadex.RemoteDesk.Clipboard;

/// <summary>
/// Panolar arası aktarımın canlı akışı. Yalnızca PC'yi O AN izleyen kullanıcıya açıktır (FileTransferCoordinator ile aynı kapı: izleme + PC bağlı); kontrol ALMAK gerekmez. Metin küçük olduğu için hub'dan geçer. İçerik LOGLANMAZ (parola olabilir).
/// </summary>
public sealed class ClipboardCoordinator
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary> Uzak panoya yazılacak metnin üst sınırı (devasa gövdeye karşı). </summary>
    private const int MaxTextLength = 1_000_000;

    private readonly ScreenStreamCoordinator _streams;
    private readonly PcConnectionRegistry _connections;
    private readonly IHubContext<PcHub, IPcHubClient> _pcHub;
    private readonly ILogger<ClipboardCoordinator> _logger;

    public ClipboardCoordinator(ScreenStreamCoordinator streams, PcConnectionRegistry connections, IHubContext<PcHub, IPcHubClient> pcHub, ILogger<ClipboardCoordinator> logger)
    {
        _streams = streams;
        _connections = connections;
        _pcHub = pcHub;
        _logger = logger;
    }

    private async Task<(bool Ok, string PcConnectionId, string? Error)> ResolveAsync(Guid deviceId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await _streams.IsViewingAsync(deviceId, userId, cancellationToken))
            return (false, string.Empty, "Pano aktarımı için PC'nin ekranını izliyor olmalısınız.");
        if (!_connections.TryGet(deviceId, out var pc))
            return (false, string.Empty, "PC merkeze bağlı değil.");
        return (true, pc.ConnectionId, null);
    }

    /// <summary> Uzak PC panosunun metnini getirir (içe aktar). </summary>
    public async Task<ClipboardResult> GetAsync(Guid deviceId, Guid userId, CancellationToken cancellationToken)
    {
        var (ok, pcConnectionId, error) = await ResolveAsync(deviceId, userId, cancellationToken);
        if (!ok)
            return new ClipboardResult { Success = false, Message = error };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ReadTimeout);
            string? text = await _pcHub.Clients.Client(pcConnectionId).GetClipboard(cts.Token);
            return new ClipboardResult { Success = true, Text = text ?? string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoteDesk: uzak pano okunamadı {DeviceId}", deviceId);
            return new ClipboardResult { Success = false, Message = "Uzak pano okunamadı (PC yanıt vermedi)." };
        }
    }

    /// <summary> Metni uzak PC panosuna yazar (dışa aktar). </summary>
    public async Task<ClipboardResult> SetAsync(Guid deviceId, Guid userId, string? text, CancellationToken cancellationToken)
    {
        var (ok, pcConnectionId, error) = await ResolveAsync(deviceId, userId, cancellationToken);
        if (!ok)
            return new ClipboardResult { Success = false, Message = error };

        text ??= string.Empty;
        if (text.Length > MaxTextLength)
            return new ClipboardResult { Success = false, Message = "Metin çok uzun (en fazla 1 MB)." };

        try
        {
            await _pcHub.Clients.Client(pcConnectionId).SetClipboard(new SetClipboardCommand(text));
            return new ClipboardResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoteDesk: uzak panoya yazılamadı {DeviceId}", deviceId);
            return new ClipboardResult { Success = false, Message = "Uzak panoya yazılamadı." };
        }
    }
}
