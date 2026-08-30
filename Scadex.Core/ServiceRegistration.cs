using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Caching;
using Scadex.Core.Utils.CriticalData;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Localization;
using Scadex.Core.Utils.Logging;
using Scadex.Core.Utils.Validation;
using Serilog;
using Serilog.Events;
using System.Globalization;

namespace Scadex.Core;

public static class ServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, WebApplicationBuilder builder)
    {

        services.AddHttpContextAccessor();
        services.AddSingleton<IHttpContextManager, HttpContextManager>();

        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            MaxDepth = 7,
            ContractResolver = new IgnoreCriticalDataResolver()
        };

        #region DISTRIBUTED CACHE
        var cacheSettings = builder.Configuration.GetSection("CacheSettings").Get<CacheSettings>() ?? new();

        services.AddSingleton(cacheSettings);

        services.AddDistributedMemoryCache();
        // services.AddStackExchangeRedisCache(options =>
        // {
        //     options.Configuration = configuration["Redis:ConnectionString"];
        // });

        services.AddSingleton<ICacheService, CacheService>();
        #endregion

        #region LOCALIZATION
        var localizationConfigirationRaw = builder.Configuration.GetSection("LocalizationSettings").Get<LocalizationSettingsConfigirationRaw>() ?? new();
        var localizationSettings = localizationConfigirationRaw.ToLocalizationSettings();

        services.AddSingleton(localizationSettings);

        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Utils/Localization/Resources";
        });

        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = localizationSettings.AvailableLanguages.Select(lang => new CultureInfo(lang.GetDescription())).ToArray();

            options.DefaultRequestCulture = new RequestCulture(localizationSettings.DefaultLanguage.GetDescription());
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new QueryStringRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider(),
            ];
        });
        #endregion

        #region LOGGING
        services.AddSingleton<ILoggingService, LoggingService>();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
                .WriteTo.Async(a => a.File(
                    path: "Logs/Information.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 100,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}"
                )))
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
                .WriteTo.Async(a => a.File(
                    path: "Logs/Warning.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 100,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}"
                )))
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error || e.Level == LogEventLevel.Fatal)
                .WriteTo.Async(a => a.File(
                    path: "Logs/Error.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 100,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}"
                )))
            .CreateLogger();

        builder.Host.UseSerilog();
        #endregion

        #region VALIDATION
        services.AddScoped<IValidationService, ValidationService>();
        #endregion

        return services;
    }
}