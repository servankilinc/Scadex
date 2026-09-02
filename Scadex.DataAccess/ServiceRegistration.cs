using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Concrete;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Interceptors;
using Scadex.DataAccess.UoW;

namespace Scadex.DataAccess;

public static class ServiceRegistration
{
    public static IServiceCollection AddDataAccessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICabinetRepository, CabinetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IDeviceCommandRepository, DeviceCommandRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IIoChannelRepository, IoChannelRepository>();
        services.AddScoped<IPinRepository, PinRepository>();
        services.AddScoped<ICanvasSettingsRepository, CanvasSettingsRepository>();
        services.AddScoped<IComponentTemplateRepository, ComponentTemplateRepository>();
        services.AddScoped<IComponentTemplatePinRepository, ComponentTemplatePinRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDiagramAnnotationRepository, DiagramAnnotationRepository>();
        services.AddScoped<ICameraRepository, CameraRepository>();
        services.AddScoped<ICameraCaptureRepository, CameraCaptureRepository>();
        services.AddScoped<IChannelEventRepository, ChannelEventRepository>();
        services.AddScoped<IDeviceStatusRepository, DeviceStatusRepository>();
        services.AddScoped<IDeviceTypeRepository, DeviceTypeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        #region DB CONTEXT
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<EntityLifecycleInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, opt) =>
        {
            opt.UseSqlServer(configuration.GetConnectionString("Database"))
               .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
               // .AddInterceptors(serviceProvider.GetRequiredService<ArchiveInterceptor>())
               .AddInterceptors(serviceProvider.GetRequiredService<EntityLifecycleInterceptor>());
        });
        #endregion

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}