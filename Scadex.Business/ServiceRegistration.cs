using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Business.Abstract;
using Scadex.Business.Concrete;
using Scadex.Business.Utils.CameraProtocolProfile;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Business.Utils.MonitoredAssetService;
using Scadex.Business.Utils.OutputPolarity;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Business.Utils.TokenService;

namespace Scadex.Business;

public static class ServiceRegistration
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        #region ENTITY SERVICES
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IDeviceCommandService, DeviceCommandService>();
        services.AddScoped<IIoChannelService, IoChannelService>();
        services.AddScoped<ICanvasSettingsService, CanvasSettingsService>();
        services.AddScoped<IComponentTemplateService, ComponentTemplateService>();
        #endregion

        services.AddScoped<ICardReadService, CardReadService>();
        services.AddScoped<IDeviceCredentialService, DeviceCredentialService>();   // saha cihazlarinin Basic Auth kimligi (DeviceAuthentication)

        services.AddScoped<IDiagramService, DiagramService>();
        services.AddScoped<IChannelEventService, ChannelEventService>();
        services.AddScoped<IAutomationStatusService, AutomationStatusService>();
        services.AddScoped<ICameraService, CameraService>();
        services.AddScoped<IScadaCommandGateway, ScadaCommandGateway>();
        services.AddScoped<IOutputPolarityResolver, OutputPolarityResolver>();

        // Yoklanabilir her entity servisi için buraya bir kayıt ekle;
        services.AddScoped<IMonitoredAssetProbeSource, DeviceProbeSource>();
        services.AddScoped<IMonitoredAssetProbeSource, ControlCardProbeSource>();     // kontrol karti: TCP/ICMP yerine /status okumasi + kanal senkronu

        #region KAMERA / MEDYA
        services.AddSingleton<ICameraProtocolProfile, HikvisionProtocolProfile>();
        services.AddSingleton<ICameraProtocolProfileResolver, CameraProtocolProfileResolver>();

        // Medya gecidi cekirdektedir: CameraManagement (cam_) ve RemoteDesk (pc_) ortak kullanir. Kare/klip cekimi modüldedir.
        services.AddScoped<IMediaGateway, MediaMtxGateway>();
        #endregion

        return services;
    }
}