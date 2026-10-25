using Serilog;
using Serilog.Events;

namespace Scadex.RemoteDesk.Windows.Service.Shell;

/// <summary>
/// Servisin ve Agent'ın dosya günlüğü: <c>%ProgramData%\Scadex\RemoteDesk\logs\{service|agent}-YYYYMMDD.log</c>. İkisi de SYSTEM
/// çalışır — SYSTEM'in <c>LocalAppData</c>'sı systemprofile olurdu. Klasör <c>%ProgramData%</c>'nın varsayılan izinleriyle kalır. FFmpeg adresleri içinde bilet taşır — günlüğe yalnızca <c>FailureExplainer.Mask</c>'ten geçmiş metin yazılır; sır hiç yazılmaz.
/// </summary>
public static class AgentLog
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Scadex", "RemoteDesk", "logs");

    /// <param name="name"><c>service</c> ya da <c>agent</c>.</param>
    public static void Configure(LoggerConfiguration config, string name) => config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.File(
            Path.Combine(Folder, $"{name}-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            // Oturum değişiminde eski Agent kapanırken yenisi açılır: kısa süre ikisi aynı dosyaya yazabilir.
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({ProcessId}) {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .Enrich.WithProperty("ProcessId", Environment.ProcessId);
}
