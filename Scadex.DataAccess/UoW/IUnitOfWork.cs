using Scadex.DataAccess.Abstract;

namespace Scadex.DataAccess.UoW;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    #region Repositories
    ICompanyRepository Companies { get; }
    ICabinetRepository Cabinets { get; }
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IRolePermissionRepository RolePermissions { get; }
    IPermissionRepository Permissions { get; }
    IDeviceCommandRepository DeviceCommands { get; }
    IConnectionRepository Connections { get; }
    IIoChannelRepository IoChannels { get; }
    IPinRepository Pins { get; }
    ICanvasSettingsRepository CanvasSettings { get; }
    IComponentTemplateRepository ComponentTemplates { get; }
    IComponentTemplatePinRepository ComponentTemplatePins { get; }
    IDeviceRepository Devices { get; }
    IDiagramAnnotationRepository DiagramAnnotations { get; }
    IDeviceStatusRepository DeviceStatuses { get; }
    IDeviceTypeRepository DeviceTypes { get; }
    ICameraRepository Cameras { get; }
    ICameraCaptureRepository CameraCaptures { get; }
    IChannelEventRepository ChannelEvents { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    #endregion

    int SaveChanges();
    void BeginTransaction();
    void CommitTransaction();
    void RollbackTransaction();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}