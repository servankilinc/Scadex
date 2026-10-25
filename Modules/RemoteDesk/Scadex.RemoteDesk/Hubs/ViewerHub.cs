using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Control;
using Scadex.RemoteDesk.Model.Dtos.Clipboard;
using Scadex.RemoteDesk.Model.Dtos.Control;
using Scadex.RemoteDesk.Model.Dtos.FileTransfer;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Hubs;

/// <summary> Sunucu → tarayıcı. </summary>
public interface IViewerHubClient
{
    Task ControlEnded(ControlEndedNotice notice);

    /// <summary> Kontrol edilen PC'de girdinin durumu değişti (kilit ekranı / UAC, red, monitör yok). </summary>
    Task ControlInputState(ControlInputNotice notice);

    /// <summary> Dosya transferinin durumu değişti (hazır/tamamlandı/başarısız). </summary>
    Task FileTransferState(Model.Dtos.FileTransfer.FileTransferStateNotice notice);
}

/// <summary>
/// Tarayıcı ↔ merkez kanalı — <c>/hubs/remote-desk/viewer</c>. Kullanıcı JWT'si query string'den okunur
/// (<c>/hubs</c> kuralı). Bağlanmak için en az <c>RemotePcView</c> gerekir (sınıf politikası); kontrol metotları ayrıca
/// <c>RemotePcControl</c>, dosya metotları <c>RemotePcFileTransfer</c> izni ister (metot politikaları, VE ile birleşir). Görüntü bu
/// hub'dan geçmez (WHEP). Yalnızca modül açıkken eşlenir.
/// </summary>
[Authorize(Policy = RemoteDeskModule.ViewPolicy)]
[DisableRateLimiting]
public class ViewerHub : Hub<IViewerHubClient>
{
    public const string Path = "/hubs/remote-desk/viewer";

    private readonly RemoteControlCoordinator _control;
    private readonly FileTransfer.FileTransferCoordinator _files;
    private readonly Clipboard.ClipboardCoordinator _clipboard;
    private readonly IUnitOfWork _unitOfWork;

    public ViewerHub(RemoteControlCoordinator control, FileTransfer.FileTransferCoordinator files,
        Clipboard.ClipboardCoordinator clipboard, IUnitOfWork unitOfWork)
    {
        _control = control;
        _files = files;
        _clipboard = clipboard;
        _unitOfWork = unitOfWork;
    }

    [Authorize(Policy = RemoteDeskModule.ControlPolicy)]
    public async Task<RequestControlResponse> RequestControl(Guid deviceId)
    {
        var userId = RequireUserId();

        bool isPc = await _unitOfWork.Devices.GetAsync(
            select: d => d.Id,
            where: d => d.Id == deviceId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: Context.ConnectionAborted) != Guid.Empty;
        if (!isPc)
            return new RequestControlResponse { Status = ControlRequestStatus.NotFound, Message = "PC bulunamadı." };

        return await _control.RequestAsync(deviceId, userId, await GetUserNameAsync(userId), Context.ConnectionId, Context.ConnectionAborted);
    }

    /// <summary> ~60 Hz paket. Tarayıcı bunu yanıt beklemeden gönderir (<c>send</c>); geçersiz paket sessizce yok sayılır. </summary>
    [Authorize(Policy = RemoteDeskModule.ControlPolicy)]
    public Task SendInput(InputBatch batch) => _control.InputAsync(Context.ConnectionId, batch);

    [Authorize(Policy = RemoteDeskModule.ControlPolicy)]
    public Task ReleaseControl(Guid controlSessionId) => _control.ReleaseAsync(Context.ConnectionId, controlSessionId);

    // Dosya transferi. Dosya izni (metot) + PC'yi O AN İZLEMEK (koordinatör) şart; kontrol ALMAK gerekmez.
    [Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
    public Task<ListDirectoryResponse> ListPcDirectory(Guid deviceId, string? path) =>
        _files.ListDirectoryAsync(Context.ConnectionId, deviceId, RequireUserId(), path, Context.ConnectionAborted);

    [Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
    public async Task<FileTransferResponse> RequestDownload(Guid deviceId, string pcPath)
    {
        var userId = RequireUserId();
        return await _files.RequestDownloadAsync(Context.ConnectionId, deviceId, userId, await GetUserNameAsync(userId), pcPath, Context.ConnectionAborted);
    }

    [Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
    public async Task<FileTransferResponse> RequestUpload(Guid deviceId, Guid transferId, string destinationPath)
    {
        var userId = RequireUserId();
        return await _files.RequestUploadAsync(Context.ConnectionId, deviceId, userId, await GetUserNameAsync(userId), transferId, destinationPath, Context.ConnectionAborted);
    }

    // Panolar arası aktarım (RemoteDesk.md § 12.9). Dosya transferiyle aynı yetki/kapı: dosya izni + PC'yi izlemek.
    [Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
    public Task<ClipboardResult> GetRemoteClipboard(Guid deviceId) =>
        _clipboard.GetAsync(deviceId, RequireUserId(), Context.ConnectionAborted);

    [Authorize(Policy = RemoteDeskModule.FileTransferPolicy)]
    public Task<ClipboardResult> SetRemoteClipboard(Guid deviceId, string text) =>
        _clipboard.SetAsync(deviceId, RequireUserId(), text, Context.ConnectionAborted);

    private Guid RequireUserId() =>
        Guid.TryParse(Context.UserIdentifier, out var userId) ? userId : throw new HubException("Kullanıcı kimliği okunamadı.");

    /// <summary> PC'deki göstergede ve denetim kaydında görünen ad. </summary>
    private async Task<string> GetUserNameAsync(Guid userId)
    {
        string? fullName = await _unitOfWork.Users.GetAsync(
            select: u => u.FullName,
            where: u => u.Id == userId,
            cancellationToken: Context.ConnectionAborted);
        return !string.IsNullOrWhiteSpace(fullName) ? fullName : Context.User?.FindFirstValue(ClaimTypes.Name) ?? userId.ToString();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _control.OnViewerDisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
