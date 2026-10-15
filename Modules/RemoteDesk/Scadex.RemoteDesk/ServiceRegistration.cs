using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Business.Utils.MediaGateway;
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
    /// PC izleme izni (<c>Permission.RemotePcView</c>) — policy adı izin koduyla aynıdır. Policy'ler çekirdekte <c>AddPermissionPolicies</c>
    /// ile her izin için kaydedilir; modül ayrıca tanımlamaz. Rol izni değişince kullanıcı yeniden giriş yapmalı ya da token'ı tazelenmeli.
    /// </summary>
    public const string ViewPolicy = nameof(Permission.RemotePcView);

    /// <summary> Uzaktan kontrol izni (<c>Permission.RemotePcControl</c>) — viewer hub'ı ister. İzlemeden ayrıdır; kontrol için izlemek de gerekir. </summary>
    public const string ControlPolicy = nameof(Permission.RemotePcControl);

    /// <summary> Denetim raporu izni (<c>Permission.RemotePcAudit</c>) — rapor endpointleri için. İzlemeden ayrıdır: izleyen operatör başkalarının geçmişini görmez. </summary>
    public const string AuditPolicy = nameof(Permission.RemotePcAudit);

    /// <summary> Dosya transferi izni (<c>Permission.RemotePcFileTransfer</c>) — en hassas yetenek; izleme/kontrolden ayrı verilir. </summary>
    public const string FileTransferPolicy = nameof(Permission.RemotePcFileTransfer);
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

        #region DOSYA TRANSFERI
        // Geçici dosyalar + bilet bellekte/diskte (singleton); DB yalnızca denetim. Süpürmesi ScreenStreamWorker'dadır.
        services.AddSingleton<FileTransfer.FileTransferStore>();
        services.AddSingleton<FileTransfer.FileTransferLogStore>();
        services.AddSingleton<FileTransfer.FileTransferCoordinator>();
        #endregion

        #region PANO
        services.AddSingleton<Clipboard.ClipboardCoordinator>();
        // Pano metni hub'dan geçer. Varsayılan 32 KB alım sınırı büyük metne yetmez; bu iki RemoteDesk hub'ında yükseltilir
        // (mantıksal sınır 1.000.000 karakter; UTF-8 en kötü 4 bayt → 4 MB başlık payıyla rahat). Diğer hub'lar varsayılanda kalır.
        services.AddSignalR()
            .AddHubOptions<Hubs.ViewerHub>(o => o.MaximumReceiveMessageSize = 4 * 1024 * 1024)
            .AddHubOptions<Hubs.PcHub>(o => o.MaximumReceiveMessageSize = 4 * 1024 * 1024);
        #endregion

        #region RAPOR
        // Denetim tablolarını (ScreenViewLog, RemoteControlSession) okur; adlar çekirdekten projeksiyonla çözülür.
        services.AddScoped<IRemoteAccessReportService, RemoteAccessReportService>();
        #endregion

        #region SERVISLER
        services.AddScoped<IPcDirectoryService, PcDirectoryService>();
        services.AddScoped<IScreenViewService, ScreenViewService>();
        #endregion

        return services;
    }
}
