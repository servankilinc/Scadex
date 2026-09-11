using FluentValidation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scadex.Business.Utils.ScadaEvents;
using Scadex.DataAccess.Interceptors;
using Scadex.Signalization.Data;
using Scadex.Signalization.Engine;
using Scadex.Signalization.Runtime;
using Scadex.Signalization.Services.Abstract;
using Scadex.Signalization.Services.Concrete;

namespace Scadex.Signalization;

public static class SignalizationModule
{
    public const string AppSettingsEnabledKey = "Modules:Signalization:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(AppSettingsEnabledKey);
}

public static class ServiceRegistration
{
    public static IMvcBuilder AddSignalizationModule(this IMvcBuilder mvc, IConfiguration configuration)
    {
        if (SignalizationModule.IsEnabled(configuration))
        {
            mvc.Services.AddSignalizationServices(configuration);
        }
        else
        {
            var moduleAssembly = typeof(ServiceRegistration).Assembly;

            // IsEnabled false olsa bile "moduleAssembly" WebAPI'nin referansı old için controller'lar otomatik keşfedilir ve route eşleşmesi
            // halinde ilgili action metoda arkaplanda istek düşer di ve openapi/swagger enpointleri varmış gibi sunar dı.
            // Servisler eklenmediği için her istekte DI hatasiyla 500 donerdi. Aşağıda çıkartılınca 404 doner ve OpenAPI'de görunmezler.
            mvc.ConfigureApplicationPartManager(manager =>
            {
                var parts = manager.ApplicationParts.OfType<AssemblyPart>().Where(p => p.Assembly == moduleAssembly).ToList();

                foreach (var part in parts)
                    manager.ApplicationParts.Remove(part);
            });
        }

        return mvc;
    }

    private static IServiceCollection AddSignalizationServices(this IServiceCollection services, IConfiguration configuration)
    {
        #region DB CONTEXT
        string connectionString = configuration.GetConnectionString("Database") ?? 
            throw new InvalidOperationException("ConnectionStrings:Signalization ya da ConnectionStrings:Database tanimli degil.");

        services.AddDbContext<SignalizationDbContext>((serviceProvider, opt) =>
        {
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SignalizationDbContext.Schema))
               .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
               // .AddInterceptors(serviceProvider.GetRequiredService<ArchiveInterceptor>())
               .AddInterceptors(serviceProvider.GetRequiredService<EntityLifecycleInterceptor>());
        });
        #endregion

        // Modulun DTO dogrulayicilari
        services.AddValidatorsFromAssembly(typeof(ServiceRegistration).Assembly);

        #region CALISMA ZAMANI
        // Kuyruklar SINGLETON olmak zorunda: dinleyici ve motor scoped'dir, kuyruk istekler arasi paylasilir.
        services.AddSingleton<SignalEventQueue>();
        services.AddSingleton<EntrySnapshotQueue>();

        // Scadex Core Comunications
        services.AddScoped<IScadaEventObserver, SignalizationScadaObserver>();
        services.AddScoped<OperatorSessionEngine>();

        services.AddHostedService<SignalEventWorker>();
        services.AddHostedService<EntrySnapshotWorker>();
        services.AddHostedService<SignalTimerWorker>();
        #endregion

        #region SERVISLER
        services.AddScoped<ISignalAuthorityService, SignalAuthorityService>();
        services.AddScoped<ISignalOperatorService, SignalOperatorService>();
        services.AddScoped<ISignalCabinetService, SignalCabinetService>();
        services.AddScoped<IOperatorSessionService, OperatorSessionService>();
        #endregion

        return services;
    }
}
