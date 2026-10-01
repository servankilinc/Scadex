using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.Auth;
using Scadex.RemoteDesk.BackgroundServices;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Control;
using Scadex.RemoteDesk.DataAccess;
using Scadex.RemoteDesk.Hubs;
using Scadex.RemoteDesk.Media;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Services.Abstract;
using Scadex.RemoteDesk.Services.Concrete;
using Scadex.RemoteDesk.Streaming;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk;

public static class RemoteDeskModule
{
    public const string AppSettingsEnabledKey = RemoteDeskOptions.SectionName + ":Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(AppSettingsEnabledKey);

    /// <summary>
    /// PC izleme izni (<c>Permission.RemotePcView</c>) — policy adı izin koduyla aynıdır. Token'daki <c>permission</c> claim'inden okunur;
    /// rol izni değişince kullanıcı yeniden giriş yapmalı ya da token'ı tazelenmeli (claim'ler token'a girişte yazılır).
    /// </summary>
    public const string ViewPolicy = nameof(Permission.RemotePcView);

    /// <summary> Uzaktan kontrol izni (<c>Permission.RemotePcControl</c>) — viewer hub'ı ister. İzlemeden ayrıdır; kontrol için izlemek de gerekir. </summary>
    public const string ControlPolicy = nameof(Permission.RemotePcControl);
}

public static class ServiceRegistration
{
    /// <summary>
    /// Açıkken servisleri kaydeder; kapalıyken modülün controller'larını ApplicationPart listesinden çıkarır (uçlar 404, OpenAPI'de yok).
    /// Yalnızca servisleri koşullamak yetmez: assembly WebAPI'nin referansı olduğu için controller'lar yine keşfedilir ve DI hatasıyla 500 döner.
    /// </summary>
    public static IMvcBuilder AddRemoteDeskModule(this IMvcBuilder mvc, IConfiguration configuration)
    {
        if (RemoteDeskModule.IsEnabled(configuration))
        {
            mvc.Services.AddRemoteDeskServices(configuration);
        }
        else
        {
            var moduleAssembly = typeof(ServiceRegistration).Assembly;
            mvc.ConfigureApplicationPartManager(manager =>
            {
                var parts = manager.ApplicationParts.OfType<AssemblyPart>().Where(p => p.Assembly == moduleAssembly).ToList();

                foreach (var part in parts)
                    manager.ApplicationParts.Remove(part);
            });
        }

        return mvc;
    }

    /// <summary>
    /// Modülün hub'larını eşler: <c>/hubs/remote-desk/pc</c> (Windows istemcileri) ve <c>/hubs/remote-desk/viewer</c> (tarayıcı, uzaktan kontrol).
    /// Kapalıyken eşlenmez → negotiate 404
    /// (servisleri kayıtlı olmadığı için eşlenseydi her bağlantıda DI hatası olurdu).
    /// </summary>
    public static IEndpointRouteBuilder MapRemoteDeskModule(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        if (RemoteDeskModule.IsEnabled(configuration))
        {
            app.MapHub<PcHub>(PcHubContract.Path);
            app.MapHub<ViewerHub>(ViewerHub.Path);
        }

        return app;
    }

    private static IServiceCollection AddRemoteDeskServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RemoteDeskOptions>().Bind(configuration.GetSection(RemoteDeskOptions.SectionName));

        #region YETKI
        // Policy modülün kaydında tanımlanır: modül kapalıyken controller'lar da çıkarıldığı için tanımsız policy'ye çarpan uç kalmaz.
        services.AddAuthorizationBuilder()
            .AddPolicy(RemoteDeskModule.ViewPolicy, policy => policy.RequireClaim(AppClaimTypes.Permission, RemoteDeskModule.ViewPolicy))
            .AddPolicy(RemoteDeskModule.ControlPolicy, policy => policy.RequireClaim(AppClaimTypes.Permission, RemoteDeskModule.ControlPolicy));
        #endregion

        #region DB CONTEXT
        string connectionString = configuration.GetConnectionString("Database") ??
            throw new InvalidOperationException("ConnectionStrings:Database tanimli degil.");

        services.AddDbContext<RemoteDeskDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", RemoteDeskDbContext.Schema)));
        #endregion

        #region BAGLANTILAR
        // Bağlı PC'ler bellekte (tek sunucu varsayımı); tabloya yazılmaz.
        services.AddSingleton<PcConnectionRegistry>();
        #endregion

        #region MEDYA
        // Biletler cache'te; yetkilendirici MediaMTX auth kancasinin sicak yolunda (IMediaPathAuthorizer — cekirdek modulu bilmez).
        services.AddSingleton<ScreenTicketStore>();
        services.AddSingleton<RemoteDeskNetworkPolicy>();
        services.AddScoped<IMediaPathAuthorizer, PcMediaPathAuthorizer>();
        #endregion

        #region YAYIN VE IZLEME
        // Yayınlar + izleyici kiralamaları bellekte (singleton); DB yalnızca denetim kaydı.
        services.AddSingleton<ScreenSessionStore>();
        services.AddSingleton<ScreenStreamCoordinator>();
        services.AddHostedService<ScreenStreamWorker>();
        #endregion

        #region UZAKTAN KONTROL
        // Kontrol eden kullanıcı bellekte (PC başına tek); DB yalnızca denetim. Süpürmesi ScreenStreamWorker'dadır.
        services.AddSingleton<RemoteControlStore>();
        services.AddSingleton<RemoteControlCoordinator>();
        #endregion

        #region SERVISLER
        services.AddScoped<IPcDirectoryService, PcDirectoryService>();
        services.AddScoped<IScreenViewService, ScreenViewService>();
        #endregion

        return services;
    }
}
