using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Hubs;
using Scadex.RemoteDesk.Model.Dtos.FileTransfer;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Streaming;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.FileTransfer;

/// <summary>
/// Dosya transferinin canlı akışını yönetir (RemoteDesk.md § 13). Baytlar buradan GEÇMEZ — bu sınıf yalnızca izleme hakkını
/// doğrular, PC'ye komut gönderir, PC sonucunu işler ve tarayıcıya durum bildirir. Baytlar biletli HTTP ile <see cref="FileTransferStore"/>
/// üzerinden akar. Her işlem yalnızca PC'nin ekranını O AN izleyen kullanıcıya açıktır kontrol ALMAK gerekmez; her transfer  denetime yazılır.
/// </summary>
public sealed class FileTransferCoordinator
{
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);

    private readonly ScreenStreamCoordinator _streams;
    private readonly PcConnectionRegistry _connections;
    private readonly FileTransferStore _store;
    private readonly FileTransferLogStore _audit;
    private readonly IHubContext<PcHub, IPcHubClient> _pcHub;
    private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub;
    private readonly ILogger<FileTransferCoordinator> _logger;

    public FileTransferCoordinator(ScreenStreamCoordinator streams, PcConnectionRegistry connections, FileTransferStore store,
        FileTransferLogStore audit, IHubContext<PcHub, IPcHubClient> pcHub, IHubContext<ViewerHub, IViewerHubClient> viewerHub,
        ILogger<FileTransferCoordinator> logger)
    {
        _streams = streams;
        _connections = connections;
        _store = store;
        _audit = audit;
        _pcHub = pcHub;
        _viewerHub = viewerHub;
        _logger = logger;
    }

    /// <summary>
    /// İşlem hakkını doğrular: kullanıcı PC'nin ekranını izliyor olmalı ("görmeden iş yapılmaz") ve PC bağlı olmalı. Başarılıysa PC
    /// bağlantı kimliğini döner; aksi hâlde neden. Kontrol (fare/klavye) ALMAK gerekmez.
    /// </summary>
    private async Task<(bool Ok, string PcConnectionId, string? Error)> ResolveAsync(Guid deviceId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await _streams.IsViewingAsync(deviceId, userId, cancellationToken))
            return (false, string.Empty, "Dosya işlemleri için PC'nin ekranını izliyor olmalısınız.");
        if (!_connections.TryGet(deviceId, out var pc))
            return (false, string.Empty, "PC merkeze bağlı değil.");
        return (true, pc.ConnectionId, null);
    }

    /// <summary> PC'de bir klasörü (ya da sürücü köklerini) listeler. Yalnızca PC'yi izleyen kullanıcı. </summary>
    public async Task<ListDirectoryResponse> ListDirectoryAsync(string viewerConnectionId, Guid deviceId, Guid userId, string? path, CancellationToken cancellationToken)
    {
        var (ok, pcConnectionId, error) = await ResolveAsync(deviceId, userId, cancellationToken);
        if (!ok)
            return new ListDirectoryResponse { Allowed = false, Message = error };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ListTimeout);
            var listing = await _pcHub.Clients.Client(pcConnectionId)
                .ListDirectory(new ListDirectoryCommand(path), cts.Token);

            return new ListDirectoryResponse
            {
                Allowed = true,
                Path = listing.Path,
                IsRoot = listing.IsRoot,
                ParentPath = listing.ParentPath,
                Error = listing.Error,
                Entries = [.. listing.Entries.Select(e => new DirEntryDto
                {
                    Name = e.Name, IsDirectory = e.IsDirectory, Size = e.Size, ModifiedUtc = e.ModifiedUtc
                })]
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoteDesk: dizin listelenemedi {DeviceId} {Path}", deviceId, path);
            return new ListDirectoryResponse { Allowed = true, Path = path ?? "", Error = "Klasör okunamadı (PC yanıt vermedi)." };
        }
    }

    /// <summary> PC'den tarayıcıya indirme: PC dosyayı merkeze yükler, hazır olunca tarayıcıya bildirilir. </summary>
    public async Task<FileTransferResponse> RequestDownloadAsync(string viewerConnectionId, Guid deviceId, Guid userId, string userName, string pcPath, CancellationToken cancellationToken)
    {
        var (ok, pcConnectionId, error) = await ResolveAsync(deviceId, userId, cancellationToken);
        if (!ok)
            return new FileTransferResponse { Accepted = false, Message = error };
        if (string.IsNullOrWhiteSpace(pcPath))
            return new FileTransferResponse { Accepted = false, Message = "Dosya yolu boş." };

        var entry = _store.BeginPcUpload(deviceId, userId, userName, pcPath);
        entry.ViewerConnectionId = viewerConnectionId;
        await _audit.StartAsync(entry.TransferId, deviceId, userId, FileTransferDirection.FromPc, entry.FileName, pcPath, cancellationToken);

        await _pcHub.Clients.Client(pcConnectionId)
            .UploadFile(new FileUploadCommand(entry.TransferId, pcPath, _store.IssueTicket(entry.TransferId)!));

        _logger.LogInformation("RemoteDesk: {User} {DeviceId} PC'sinden dosya indirme başlattı: {Path}", userName, deviceId, pcPath);
        return new FileTransferResponse { Accepted = true, TransferId = entry.TransferId };
    }

    /// <summary> Tarayıcıdan PC'ye yükleme: tarayıcı dosyayı merkeze yüklemiş olmalı; PC merkezden çekip hedefe yazar. </summary>
    public async Task<FileTransferResponse> RequestUploadAsync(string viewerConnectionId, Guid deviceId, Guid userId, string userName, Guid transferId, string destinationPath, CancellationToken cancellationToken)
    {
        var (ok, pcConnectionId, error) = await ResolveAsync(deviceId, userId, cancellationToken);
        if (!ok)
            return new FileTransferResponse { Accepted = false, Message = error };
        if (string.IsNullOrWhiteSpace(destinationPath))
            return new FileTransferResponse { Accepted = false, Message = "Hedef klasör boş." };
        if (!_store.TryGet(transferId, out var entry) || entry.Direction != FileTransferDirection.ToPc || entry.UserId != userId)
            return new FileTransferResponse { Accepted = false, Message = "Transfer bulunamadı." };
        if (entry.Status != FileTransferStatus.ReadyForPc)
            return new FileTransferResponse { Accepted = false, Message = "Dosya merkeze henüz tam yüklenmedi." };

        _store.BindDestination(transferId, deviceId, destinationPath);
        entry.ViewerConnectionId = viewerConnectionId;
        string fullDest = CombineDestination(destinationPath, entry.FileName);
        await _audit.StartAsync(transferId, deviceId, userId, FileTransferDirection.ToPc, entry.FileName, fullDest, cancellationToken);

        await _pcHub.Clients.Client(pcConnectionId)
            .DownloadFile(new FileDownloadCommand(transferId, destinationPath, entry.FileName, _store.IssueTicket(transferId)!));

        _logger.LogInformation("RemoteDesk: {User} {DeviceId} PC'sine dosya yükleme başlattı: {Dest}", userName, deviceId, fullDest);
        return new FileTransferResponse { Accepted = true, TransferId = transferId };
    }

    /// <summary> PC transferin sonucunu bildirdi (push/pull bitti). Denetimi kapatır, tarayıcıya durumu iletir. </summary>
    public async Task OnPcResultAsync(Guid deviceId, string pcConnectionId, FileTransferResult result)
    {
        if (!_store.TryGet(result.TransferId, out var entry) || entry.DeviceId != deviceId)
            return;

        await _audit.EndAsync(result.TransferId, result.Success, result.Bytes, result.Error);

        if (entry.Direction == FileTransferDirection.FromPc)
        {
            // PC→tarayıcı: merkezde dosya hazır; tarayıcı indirecek. Kayıt indirme bitince (GET) ya da TTL'de silinir.
            _store.SetStatus(result.TransferId, result.Success ? FileTransferStatus.ReadyForBrowser : FileTransferStatus.Failed, result.Bytes, result.Error);
            await NotifyViewerAsync(entry, result.Success ? FileTransferStatus.ReadyForBrowser : FileTransferStatus.Failed, result.Error);
            if (!result.Success) _store.Remove(result.TransferId);
        }
        else
        {
            // tarayıcı→PC: PC dosyayı yazdı; geçici dosya artık gereksiz.
            _store.SetStatus(result.TransferId, result.Success ? FileTransferStatus.Completed : FileTransferStatus.Failed, result.Bytes, result.Error);
            await NotifyViewerAsync(entry, result.Success ? FileTransferStatus.Completed : FileTransferStatus.Failed, result.Error);
            _store.Remove(result.TransferId);
        }
    }

    private async Task NotifyViewerAsync(FileTransferEntry entry, FileTransferStatus status, string? error)
    {
        if (string.IsNullOrEmpty(entry.ViewerConnectionId)) return;
        await _viewerHub.Clients.Client(entry.ViewerConnectionId).FileTransferState(new FileTransferStateNotice
        {
            TransferId = entry.TransferId,
            Status = status,
            FileName = entry.FileName,
            Bytes = entry.Bytes,
            Error = error
        });
    }

    /// <summary>
    /// "Görmeden iş yapılmaz" kuralının akış boyunca sürmesi: izleme yalnızca istek anında denetlenir; kullanıcı PC'yi
    /// izlemeyi bırakınca PC'nin dahil olduğu açık transferi burada kesilir — <c>push</c>/<c>pull</c> akışı iptal edilir, bilet kayıtla
    /// gider (başlamamış ayak 401 alır), denetim başarısız kapanır, tarayıcıya bildirilir. PC'nin sonradan gelen sonucu kayıt olmadığı
    /// için yok sayılır. Tarayıcı ayakları (merkeze yükleme, merkezden indirme) PC'ye dokunmadığı için kesilmez.
    /// <see cref="Control.RemoteControlCoordinator.SweepAsync"/> ile aynı kalıp; <c>ScreenStreamWorker</c> turunda çalışır.
    /// </summary>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in _store.Snapshot())
        {
            if (entry.DeviceId is not Guid deviceId || !IsPcLegOpen(entry))
                continue;
            if (await _streams.IsViewingAsync(deviceId, entry.UserId, cancellationToken))
                continue;

            const string reason = "İzleme bittiği için aktarım durduruldu.";
            await entry.Abort.CancelAsync();
            _store.Remove(entry.TransferId);
            await _audit.EndAsync(entry.TransferId, false, entry.Bytes, reason);
            await NotifyViewerAsync(entry, FileTransferStatus.Failed, reason);
            _logger.LogInformation("RemoteDesk: {User} {DeviceId} PC'sini izlemeyi bıraktı, dosya transferi {TransferId} kesildi",
                entry.UserName, deviceId, entry.TransferId);
        }
    }

    /// <summary> PC'nin dahil olduğu ayak açık mı: PC merkeze gönderiyor ya da PC'ye yazma komutu verilmiş (hedef bağlı). </summary>
    private static bool IsPcLegOpen(FileTransferEntry entry) => entry.Direction == FileTransferDirection.FromPc
        ? entry.Status == FileTransferStatus.PcSending
        : entry.Status == FileTransferStatus.ReadyForPc && entry.DestinationPath is not null;

    /// <summary> Hedef klasör + dosya adı (günlük/denetim için tam yol) — PC'nin yol kuralıyla, sunucununkiyle değil. </summary>
    private static string CombineDestination(string destinationPath, string fileName) => PcPathText.Join(destinationPath, fileName);
}
