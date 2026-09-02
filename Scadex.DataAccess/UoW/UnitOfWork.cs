using Microsoft.EntityFrameworkCore.Storage;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;

namespace Scadex.DataAccess.UoW;

public class UnitOfWork : IUnitOfWork
{
    private IDbContextTransaction? _transaction;
    private readonly AppDbContext _context;

    #region Repositories
    public ICompanyRepository Companies { get; private set; }
    public ICabinetRepository Cabinets { get; private set; }
    public IUserRepository Users { get; private set; }
    public IRoleRepository Roles { get; private set; }
    public IRolePermissionRepository RolePermissions { get; private set; }
    public IPermissionRepository Permissions { get; private set; }
    public IDeviceCommandRepository DeviceCommands { get; private set; }
    public IConnectionRepository Connections { get; private set; }
    public IIoChannelRepository IoChannels { get; private set; }
    public IPinRepository Pins { get; private set; }
    public ICanvasSettingsRepository CanvasSettings { get; private set; }
    public IComponentTemplateRepository ComponentTemplates { get; private set; }
    public IComponentTemplatePinRepository ComponentTemplatePins { get; private set; }
    public IDeviceRepository Devices { get; private set; }
    public IDiagramAnnotationRepository DiagramAnnotations { get; private set; }
    public IDeviceStatusRepository DeviceStatuses { get; private set; }
    public IDeviceTypeRepository DeviceTypes { get; private set; }
    public ICameraRepository Cameras { get; private set; }
    public ICameraCaptureRepository CameraCaptures { get; private set; }
    public IChannelEventRepository ChannelEvents { get; private set; }
    public IRefreshTokenRepository RefreshTokens { get; private set; }
    #endregion

    public UnitOfWork(AppDbContext context, ICompanyRepository companyRepository, ICabinetRepository cabinetRepository, IUserRepository userRepository, IRoleRepository roleRepository, IRolePermissionRepository rolePermissionRepository, IPermissionRepository permissionRepository, IDeviceCommandRepository deviceCommandRepository, IConnectionRepository connectionRepository, IIoChannelRepository ioChannelRepository, IPinRepository pinRepository, ICanvasSettingsRepository canvasSettingsRepository, IComponentTemplateRepository componentTemplateRepository, IComponentTemplatePinRepository componentTemplatePinRepository, IDeviceRepository deviceRepository, IDiagramAnnotationRepository diagramAnnotationRepository, IDeviceStatusRepository deviceStatusRepository, IDeviceTypeRepository deviceTypeRepository, ICameraRepository cameraRepository, ICameraCaptureRepository cameraCaptureRepository, IChannelEventRepository channelEventRepository, IRefreshTokenRepository refreshTokenRepository)
    {
        _context = context;
        Companies = companyRepository;
        Cabinets = cabinetRepository;
        Users = userRepository;
        Roles = roleRepository;
        RolePermissions = rolePermissionRepository;
        Permissions = permissionRepository;
        DeviceCommands = deviceCommandRepository;
        Connections = connectionRepository;
        IoChannels = ioChannelRepository;
        Pins = pinRepository;
        CanvasSettings = canvasSettingsRepository;
        ComponentTemplates = componentTemplateRepository;
        ComponentTemplatePins = componentTemplatePinRepository;
        Devices = deviceRepository;
        DiagramAnnotations = diagramAnnotationRepository;
        DeviceStatuses = deviceStatusRepository;
        DeviceTypes = deviceTypeRepository;
        Cameras = cameraRepository;
        CameraCaptures = cameraCaptureRepository;
        ChannelEvents = channelEventRepository;
        RefreshTokens = refreshTokenRepository;
    }

    public int SaveChanges()
    {
        return _context.SaveChanges();
    }

    public void BeginTransaction()
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction already started for begin transaction.");
        _transaction = _context.Database.BeginTransaction();
    }

    public void CommitTransaction()
    {
        if (_transaction == null)
            return;
        try
        {
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void RollbackTransaction()
    {
        if (_transaction == null)
            return;
        try
        {
            _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction already started for begin transaction.");
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return;
        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return;
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (_transaction != null)
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}