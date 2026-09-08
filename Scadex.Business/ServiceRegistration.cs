using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Scadex.Business.Abstract;
using Scadex.Business.Concrete;
using Scadex.Business.Utils.TokenService;
using Scadex.Business.Utils.CameraProtocolProfile;
using Scadex.Business.Utils.ClipCaptureQueue;
using Scadex.Business.Utils.SnapshotGateway;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;

namespace Scadex.Business;

public static class ServiceRegistration
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        #region ENTITY SERVICES
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICabinetService, CabinetService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IDeviceCommandService, DeviceCommandService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IIoChannelService, IoChannelService>();
        services.AddScoped<IPinService, PinService>();
        services.AddScoped<ICanvasSettingsService, CanvasSettingsService>();
        services.AddScoped<IComponentTemplateService, ComponentTemplateService>();
        services.AddScoped<IComponentTemplatePinService, ComponentTemplatePinService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDiagramAnnotationService, DiagramAnnotationService>();
        services.AddScoped<IDeviceStatusService, DeviceStatusService>();
        services.AddScoped<IDeviceTypeService, DeviceTypeService>();
        #endregion

        services.AddScoped<IDiagramService, DiagramService>();
        services.AddScoped<IChannelEventService, ChannelEventService>();
        services.AddScoped<ICameraService, CameraService>();
        services.AddScoped<IScadaCommandGateway, ScadaCommandGateway>();

        #region KAMERA / MEDYA
        services.AddSingleton(
            configuration.GetSection(Settings.MediaGatewaySettings.SectionName).Get<Settings.MediaGatewaySettings>()
            ?? new Settings.MediaGatewaySettings());

        services.AddSingleton(
            configuration.GetSection(Settings.CameraCaptureSettings.SectionName).Get<Settings.CameraCaptureSettings>()
            ?? new Settings.CameraCaptureSettings());

        services.AddSingleton<ICameraProtocolProfile, HikvisionProtocolProfile>();
        services.AddSingleton<ICameraProtocolProfileResolver, CameraProtocolProfileResolver>();

        services.AddScoped<ISnapshotGateway, IsapiSnapshotGateway>();

        // Klip kuyrugu SINGLETON olmak zorunda
        services.AddSingleton<IClipCaptureQueue, ClipCaptureQueue>();
        #endregion

        return services;
    }
}