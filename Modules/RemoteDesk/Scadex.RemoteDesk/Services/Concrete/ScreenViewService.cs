using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Model.Dtos.View.Queries;
using Scadex.RemoteDesk.Services.Abstract;
using Scadex.RemoteDesk.Streaming;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk.Services.Concrete;

/// <summary> İstekteki kullanıcıyı ve cihazı doğrular, canlı işi <see cref="ScreenStreamCoordinator"/>'a bırakır. </summary>
public class ScreenViewService : IScreenViewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ScreenStreamCoordinator _coordinator;

    public ScreenViewService(IUnitOfWork unitOfWork, IHttpContextManager httpContextManager, ScreenStreamCoordinator coordinator)
    {
        _unitOfWork = unitOfWork;
        _httpContextManager = httpContextManager;
        _coordinator = coordinator;
    }

    /// <inheritdoc />
    public async Task<Result<ScreenViewDto>> StartAsync(Guid deviceId, int monitorIndex, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId() is not { } userId)
            return Result<ScreenViewDto>.Forbidden(message: "Kullanıcı kimliği okunamadı.");

        bool isPc = await _unitOfWork.Devices.GetAsync(
            select: d => d.Id,
            where: d => d.Id == deviceId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: cancellationToken) != Guid.Empty;
        if (!isPc)
            return Result<ScreenViewDto>.NotFound(message: "PC bulunamadı.");

        return await _coordinator.StartViewAsync(deviceId, monitorIndex, userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> RenewAsync(Guid viewId, CancellationToken cancellationToken = default) =>
        CurrentUserId() is { } userId
            ? _coordinator.RenewAsync(viewId, userId, cancellationToken)
            : Task.FromResult(Result.Forbidden(message: "Kullanıcı kimliği okunamadı."));

    /// <inheritdoc />
    public Task<Result> ReleaseAsync(Guid viewId, CancellationToken cancellationToken = default) =>
        CurrentUserId() is { } userId
            ? _coordinator.ReleaseAsync(viewId, userId, cancellationToken)
            : Task.FromResult(Result.Forbidden(message: "Kullanıcı kimliği okunamadı."));

    private Guid? CurrentUserId()
    {
        var identifier = _httpContextManager.GetNameIdentifier();
        return identifier.IsSuccess && Guid.TryParse(identifier.Data, out var userId) ? userId : null;
    }
}
