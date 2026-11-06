using FluentValidation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Business.Utils.DeviceObserver;
using Scadex.Business.Utils.MediaGateway;
using Scadex.CameraManagement.BackgroundServices;
using Scadex.CameraManagement.Capture;
using Scadex.CameraManagement.DataAccess;
using Scadex.CameraManagement.Mappings;
using Scadex.CameraManagement.Media;
using Scadex.CameraManagement.Services.Abstract;
using Scadex.CameraManagement.Services.Concrete;
using Scadex.DataAccess.Interceptors;

namespace Scadex.CameraManagement;

/// <summary>
/// Yetenek modülü: kameranın canlı izlemesi, anlık görüntü, kare/klip çekimi, çekim ayarları, saklama ve MediaMTX <c>cam_</c> yolları.
/// Kameranın CİHAZ kısmı (<c>DeviceType.Camera</c>, <c>Camera</c> ek tablosu, diyagram, kamera bilgisi ekranının güncelleme ucu) ve
/// medya geçidi (<c>IMediaGateway</c>) çekirdektedir. Çözümler (Signalization) bu modüle bağımlıdır.
/// </summary>
public static class CameraManagementModule
{
    public const string AppSettingsEnabledKey = "Modules:CameraManagement:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(AppSettingsEnabledKey);
}

public static class ServiceRegistration
{
    /// <summary>
    /// Açıkken servisleri kaydeder; kapalıyken modülün controller'larını ApplicationPart listesinden çıkarır (uçlar 404, OpenAPI'de yok).
    /// Yalnızca servisleri koşullamak yetmez: assembly WebAPI'nin referansı olduğu için controller'lar yine keşfedilir ve DI hatasıyla 500 döner.
    /// </summary>
    public static IMvcBuilder AddCameraManagementModule(this IMvcBuilder mvc, IConfiguration configuration)
    {
        if (CameraManagementModule.IsEnabled(configuration))
        {
            mvc.Services.AddCameraManagementServices(configuration);
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

    private static IServiceCollection AddCameraManagementServices(this IServiceCollection services, IConfiguration configuration)
    {
        #region DB CONTEXT
        string connectionString = configuration.GetConnectionString("Database") ??
            throw new InvalidOperationException("ConnectionStrings:Database tanimli degil.");

        services.AddDbContext<CameraManagementDbContext>((serviceProvider, opt) =>
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CameraManagementDbContext.Schema))
               // CameraCaptureSetting IAuditableEntity'dir (CreatedBy / UpdatedBy...).
               .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
               .AddInterceptors(serviceProvider.GetRequiredService<EntityLifecycleInterceptor>()));
        #endregion

        // Cekirdegin AddAutoMapper kaydina EKLENIR (AutoMapper 13+ yapilandirmayi birlestirir); IMapper tektir.
        services.AddAutoMapper(typeof(CameraManagementMappingProfile).Assembly);
        services.AddValidatorsFromAssembly(typeof(ServiceRegistration).Assembly);

        #region SERVISLER
        services.AddScoped<ICameraCaptureSettingService, CameraCaptureSettingService>();
        services.AddScoped<ICameraMediaService, CameraMediaService>();
        #endregion

        #region MEDYA GECIDI KANCALARI
        // MediaMTX auth kancasinda cam_ yollari (yalnizca read + bilet). Cekirdek yolu bilmez.
        services.AddScoped<IMediaPathAuthorizer, CameraMediaPathAuthorizer>();
        // Diyagramdan / kamera ekranindan kamera bilgisi degisince (commit sonrasi) yol dusurulur.
        services.AddScoped<IDeviceChangeObserver, CameraDeviceChangeObserver>();
        #endregion

        #region CEKIM
        // Cekim dosyalarini diske yazar. SCOPED: cekim koku veritabanindaki ayardan okunuyor, o servis de scoped.
        services.AddScoped<ICaptureFileStore, CaptureFileStore>();
        // Anlik goruntu + klip: MediaMTX canli yolundan FFmpeg ile (exe yolu ContentRootPath'ten cozulur).
        services.AddScoped<ICameraCaptureGateway, CameraCaptureGateway>();
        // Klip kuyrugu SINGLETON olmak zorunda
        services.AddSingleton<IClipCaptureQueue, ClipCaptureQueue>();
        #endregion

        #region ARKA PLAN ISLERI
        // HTTP istegini klip suresi kadar bekletmek yerine is buraya duser.
        services.AddHostedService<ClipCaptureWorker>();
        // MediaMTX'te birikmis canli izleme yollarinin gunluk temizligi (yalnizca cam_).
        services.AddHostedService<MediaPathCleanupWorker>();
        // Saklama suresi dolmus cekim DOSYALARINI gunluk siler.
        services.AddHostedService<CaptureRetentionWorker>();
        #endregion

        return services;
    }
}
