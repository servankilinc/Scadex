using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Control;
using Scadex.RemoteDesk.Model.Dtos.Control;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Hubs;

/// <summary> Sunucu → tarayıcı. </summary>
public interface IViewerHubClient
{
    Task ControlEnded(ControlEndedNotice notice);
}

/// <summary>
/// Tarayıcı ↔ merkez uzaktan kontrol kanalı — <c>/hubs/remote-desk/viewer</c> (RemoteDesk.md § 12.1). Kullanıcı JWT'si query string'den okunur
/// (<c>/hubs</c> kuralı) ve <c>RemotePcControl</c> izni zorunludur; izin yoksa bağlantı kurulamaz. Görüntü bu hub'dan geçmez (WHEP).
/// Yalnızca modül açıkken eşlenir.
/// </summary>
[Authorize(Policy = RemoteDeskModule.ControlPolicy)]
[DisableRateLimiting]
public class ViewerHub : Hub<IViewerHubClient>
{
    public const string Path = "/hubs/remote-desk/viewer";

    private readonly RemoteControlCoordinator _control;
    private readonly IUnitOfWork _unitOfWork;

    public ViewerHub(RemoteControlCoordinator control, IUnitOfWork unitOfWork)
    {
        _control = control;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequestControlResponse> RequestControl(Guid deviceId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            throw new HubException("Kullanıcı kimliği okunamadı.");

        bool isPc = await _unitOfWork.Devices.GetAsync(
            select: d => d.Id,
            where: d => d.Id == deviceId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: Context.ConnectionAborted) != Guid.Empty;
        if (!isPc)
            return new RequestControlResponse { Status = ControlRequestStatus.NotFound, Message = "PC bulunamadı." };

        // PC'deki göstergede ve diğer izleyicilerin ekranında görünen ad.
        string? fullName = await _unitOfWork.Users.GetAsync(
            select: u => u.FullName,
            where: u => u.Id == userId,
            cancellationToken: Context.ConnectionAborted);
        string userName = !string.IsNullOrWhiteSpace(fullName) ? fullName : Context.User?.FindFirstValue(ClaimTypes.Name) ?? userId.ToString();

        return await _control.RequestAsync(deviceId, userId, userName, Context.ConnectionId, Context.ConnectionAborted);
    }

    /// <summary> ~60 Hz paket. Tarayıcı bunu yanıt beklemeden gönderir (<c>send</c>); geçersiz paket sessizce yok sayılır. </summary>
    public Task SendInput(InputBatch batch) => _control.InputAsync(Context.ConnectionId, batch);

    public Task ReleaseControl(Guid controlSessionId) => _control.ReleaseAsync(Context.ConnectionId, controlSessionId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _control.OnViewerDisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
