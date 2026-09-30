using Serilog;
using Serilog.Events;
using System.IO;

namespace Scadex.RemoteDesk.Windows.Services.Shell;

/// <summary>
/// İstemcinin dosya günlüğü. Klasör kullanıcı başınadır (<c>%LOCALAPPDATA%\Scadex\RemoteDesk\logs</c>): uygulama klasörü
/// (Program Files ya da elle kopyalanan klasör) yazılabilir olmayabilir ve her oturumun kendi istemcisi vardır (§ 9.1).
/// FFmpeg adresleri içinde token taşır — günlüğe yalnızca <c>FailureExplainer.Mask</c>'ten geçmiş metin yazılır.
/// </summary>
public static class ClientLog
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scadex", "RemoteDesk", "logs");

    public static void Configure(LoggerConfiguration config) => config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.File(
            Path.Combine(Folder, "client-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            // Aynı kullanıcı iki oturumda istemci açarsa (hızlı kullanıcı değiştirme) aynı dosyaya ikisi de yazabilsin.
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

    /// <summary> Günlük klasörünü Gezgin'de açar (yoksa önce oluşturur). </summary>
    public static void OpenFolder()
    {
        Directory.CreateDirectory(Folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Folder) { UseShellExecute = true });
    }
}
