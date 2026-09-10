using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scadex.Business;
using Scadex.Business.Mappings;
using Scadex.Business.Utils.CaptureFileStore;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Business.Utils.SnapshotGateway;
using Scadex.Core;
using Scadex.Core.Utils;
using Scadex.Core.Utils.Auth;
using Scadex.DataAccess;
using Scadex.DataAccess.Contexts;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Entities;
using Scadex.WebAPI.BackgroundServices;
using Scadex.WebAPI.Hubs;
using Scadex.WebAPI.Tools;
using Scadex.WebAPI.Utils;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


#region ------- CORS -------
string[] allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("policy_cors", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader()
            //.WithHeaders("Content-Type", "Authorization")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
#endregion


#region ------- Rate Limiter -------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Body'siz bir 429 dondürmek yerine, ProblemDetails ile gönderilir.
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var problemDetails = new ProblemDetails
        {
            Type = $"problems/{nameof(StatusCodes.Status429TooManyRequests)}",
            Title = "Cok fazla istek gonderildi. Lutfen biraz bekleyip tekrar deneyin.",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = string.Empty
        };
        problemDetails.Extensions["code"] = StatusCodes.Status429TooManyRequests;
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    };

    // Kullanıcı veya kullanıcı yoksa ip bazlı rate limiter uygulanır
    options.AddPolicy(RateLimiterKey.Default, httpContext =>
    {
        string? clientKey =
            httpContext.User.Identity?.IsAuthenticated == true ? (
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                httpContext.User?.Identity?.Name
            ) :
            httpContext.Connection.RemoteIpAddress?.ToString();

        string partitionKey = $"client_{clientKey ?? "unknown"}";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromSeconds(10),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // SCADA ingest'leri için rate limiting daha esnek olması için ayrı bir politika ile yönet. ScadaController'daki [EnableRateLimiting(RateLimiterKey.Scada)] 
    options.AddPolicy(RateLimiterKey.Scada, httpContext =>
    {
        string partitionKey = $"scada_:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromSeconds(5),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // Medya gecidinin kimlik dogrulama istekleri için haberleşirken AYRI politika ile yönet.
    options.AddPolicy(RateLimiterKey.MediaGateway, httpContext =>
    {
        string partitionKey = $"media_gateway_:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromSeconds(5),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});
#endregion


#region ------- Layer Registrations -------
builder.Services.AddCoreServices(builder);
builder.Services.AddDataAccessServices(builder.Configuration);
builder.Services.AddBusinessServices(builder.Configuration);
#endregion


#region ------- IDENTITY -------
builder.Services
    .AddIdentity<User, Role>(options =>
    {
        // 7 başarısız denemeden sonra 5dk istekte bulunamaz
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 7;
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedEmail = false;

        options.Password.RequiredLength = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;

        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcçdefgğhiıjklmnoöpqrsştuüvwxyzABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZ0123456789-._@+/*|!,;:()&#?[] ";
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization();
#endregion


#region ------- JWT Implementation -------
TokenSettings tokenSettings = builder.Configuration.GetSection("TokenSettings").Get<TokenSettings>()!;
if (string.IsNullOrWhiteSpace(tokenSettings.SecurityKey))
{
    Console.WriteLine("TokenSettings:SecurityKey tanımlı değil.");
    throw new InvalidOperationException("TokenSettings:SecurityKey tanımlı değil.");
}
builder.Services.AddSingleton(tokenSettings);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = tokenSettings.Issuer,
            ValidAudience = tokenSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(tokenSettings.SecurityKey)),
            // 1dk boyunca süresi geçmiş token ile devam edebilme toleransı
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        // WebSocket Authorization Header'ı taşıyamaz. Tarayicinin WebSocket API'sine ozel header verilemez;
        // 401 alnımaması için, SignalR token'i query string'e koyar. Yalnizca /hubs ile baslayan pathler icin geçerlidir.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });
#endregion


#region ------- AutoMapper -------
builder.Services.AddAutoMapper(typeof(MappingProfiles).Assembly);
#endregion


#region ------- FluentValidation -------
builder.Services.AddValidatorsFromAssembly(typeof(CabinetCreateDto).Assembly);
#endregion

builder.Services.AddExceptionHandler<ExceptionHandleMiddleware>();
builder.Services.AddProblemDetails();


#region ------- SignalR / Notifier -------
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.SetByProjectSettings());

builder.Services.AddScoped<IDiagramNotifier, DiagramNotifier>();
#endregion

#region ------- Http Clients -------
// Scada
builder.Services.AddHttpClient(IScadaCommandGateway.HttpClientName, client =>
{
    // Timeout yine SONSUZ ve zaman asimini gecit kendi CancellationTokenSource'uyla uyguluyor
    client.Timeout = Timeout.InfiniteTimeSpan;
});

// MediaGateway
builder.Services.AddHttpClient(ISnapshotGateway.HttpClientName, client =>
{
    // Timeout yine SONSUZ ve zaman asimini gecit kendi CancellationTokenSource'uyla uyguluyor
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddHttpClient(IMediaGateway.HttpClientName);
#endregion


#region ------- HostedService -------
builder.Services.AddHostedService<OfflineDeviceChecker>();

// HTTP istegini Klip, suresi kadar bekletmek yerine iş buraya dusuyor.
builder.Services.AddHostedService<ClipCaptureWorker>();

// IMonitoredAsset uygulayan varliklarin TCP yoklamasi.
builder.Services.AddHostedService<MonitoredAssetProbeWorker>();

// MediaMTX'te birikmis canli izleme yollarinin gunluk temizligi.
builder.Services.AddHostedService<MediaPathCleanupWorker>();

// Saklama suresi dolmus cekim DOSYALARININ gunluk siler.
builder.Services.AddHostedService<CaptureRetentionWorker>();
#endregion

#region ------- Kamera / medya gecidi -------
// Cekim dosyalarinin diske yazan servis.
// SCOPED: cekim koku artik veritabanindaki ayardan okunuyor, o servis de scoped.
builder.Services.AddScoped<ICaptureFileStore, CaptureFileStore>();
#endregion


builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.SetByProjectSettings());

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ScalarSecuritySchemeTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();

app.UseStaticFiles();

//if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("policy_cors");

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.MapHub<DiagramHub>("/hubs/diagram");

app.Run();
