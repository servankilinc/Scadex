using System.Security.Principal;
using Microsoft.Extensions.Hosting.WindowsServices;
using Scadex.RemoteDesk.Windows.Service.Agent;
using Scadex.RemoteDesk.Windows.Service.Services;
using Scadex.RemoteDesk.Windows.Service.Services.Clipboard;
using Scadex.RemoteDesk.Windows.Service.Services.Connection;
using Scadex.RemoteDesk.Windows.Service.Services.Desktop;
using Scadex.RemoteDesk.Windows.Service.Services.Encoding;
using Scadex.RemoteDesk.Windows.Service.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Service.Services.FileTransfer;
using Scadex.RemoteDesk.Windows.Service.Services.Input;
using Scadex.RemoteDesk.Windows.Service.Services.Monitors;
using Scadex.RemoteDesk.Windows.Service.Services.Streaming;
using Scadex.RemoteDesk.Windows.Service.Shell;
using Scadex.RemoteDesk.Windows.Service.Supervisor;
using Serilog;

// Tek exe, iki mod: argümansız = Windows servisi (gözetmen), --session = Agent (kullanıcı oturumunda, SYSTEM).
bool agentMode = args.Contains(AgentLauncher.SessionArgument, StringComparer.OrdinalIgnoreCase);

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [.. args.Where(a => !a.Equals(AgentLauncher.SessionArgument, StringComparison.OrdinalIgnoreCase))],
    // İçerik kökü exe'nin klasörüdür: servis System32'de, Agent servisin verdiği klasörde başlar; appsettings.json exe'nin yanındadır.
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddSerilog((_, config) => AgentLog.Configure(config, agentMode ? "agent" : "service"));

if (agentMode)
    ConfigureAgent(builder);
else
    ConfigureService(builder);

var host = builder.Build();
var log = host.Services.GetRequiredService<ILogger<Program>>();
AppDomain.CurrentDomain.UnhandledException += (_, e) => log.LogCritical(e.ExceptionObject as Exception, "İşlenmeyen hata, süreç kapanıyor");
TaskScheduler.UnobservedTaskException += (_, e) => log.LogError(e.Exception, "Gözlenmeyen görev hatası");

// Agent yalnızca servisin başlattığı SYSTEM sürecidir. Geliştirmede (DOTNET_ENVIRONMENT=Development) normal kullanıcıyla da çalışır.
if (agentMode && !WindowsIdentity.GetCurrent().IsSystem && !builder.Environment.IsDevelopment())
{
    log.LogError("Agent yalnızca servis tarafından SYSTEM olarak başlatılır; elle çalıştırılamaz.");
    return 2;
}

try
{
    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    // Sıfır olmayan çıkış: servis modunda SCM kurtarma (yeniden başlatma) tetiklenir, Agent modunda servis yeniden başlatır.
    log.LogCritical(ex, "{Mode} beklenmedik şekilde durdu", agentMode ? "Agent" : "Servis");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ConfigureService(HostApplicationBuilder builder)
{
    builder.Services.AddWindowsService(o => o.ServiceName = "ScadexRemoteDesk");
    builder.Services.AddSingleton<SessionChangeQueue>();
    // Oturum olaylarını SCM'den alabilmek için standart servis yaşam döngüsü genişletilir (yalnızca gerçekten servis olarak çalışırken).
    if (WindowsServiceHelpers.IsWindowsService())
        builder.Services.AddSingleton<IHostLifetime, SessionAwareServiceLifetime>();
    builder.Services.AddSingleton<AgentLauncher>();
    builder.Services.AddHostedService<SessionSupervisorService>();
}

static void ConfigureAgent(HostApplicationBuilder builder)
{
    var services = builder.Services;
    services.Configure<RemoteDeskClientOptions>(builder.Configuration.GetSection(RemoteDeskClientOptions.Section));

    services.AddSingleton<SessionContext>();
    services.AddSingleton<ChildProcessJob>();   // tek Job Object: Agent ölünce tüm FFmpeg'ler ölür
    services.AddSingleton<InputDesktopWatcher>();   // UAC / kilit ekranı: yayın ve girdi aktif masaüstüne geçer (Faz 11b)
    services.AddSingleton<IFfmpegLocator, FfmpegLocator>();
    services.AddSingleton<IMonitorService, MonitorService>();
    services.AddSingleton<IEncoderProbeService, EncoderProbeService>();
    services.AddSingleton<IScreenStreamService, ScreenStreamService>();

    // Uzaktan kontrol: tek örnek hem IRemoteInputService (kuyruk, durum) hem hosted service (SendInput işçisi).
    services.AddSingleton<RemoteInputService>();
    services.AddSingleton<IRemoteInputService>(sp => sp.GetRequiredService<RemoteInputService>());
    services.AddHostedService(sp => sp.GetRequiredService<RemoteInputService>());

    // Dosya transferi (yol politikası SYSTEM için) ve pano (kendi STA iş parçacığı).
    services.AddSingleton<FilePathPolicy>();
    services.AddSingleton<IFileTransferService, FileTransferService>();
    services.AddSingleton<IClipboardAccess, ClipboardAccess>();

    // Merkez bağlantısı: tek örnek hem hosted service (döngü) hem ICentralConnection (durum, "yeniden bağlan").
    services.AddSingleton<CentralConnectionService>();
    services.AddSingleton<ICentralConnection>(sp => sp.GetRequiredService<CentralConnectionService>());
    services.AddHostedService(sp => sp.GetRequiredService<CentralConnectionService>());

    // Arayüz: durum özeti + named pipe.
    services.AddSingleton<AgentStateService>();
    services.AddHostedService<AgentPipeServer>();

    // EN SON: ters sırada ilk durur — yayınlar bağlantı kapanmadan nazikçe kapansın.
    services.AddHostedService<AgentLifetimeService>();
}
