using System.IO;
using Serilog;
using Serilog.Events;

namespace Scadex.RemoteDesk.Windows.Services.Shell;

/// <summary>
/// Arayüzün kendi günlüğü (yalnızca arayüz olayları): <c>%LOCALAPPDATA%\Scadex\RemoteDesk\logs\ui-YYYYMMDD.log</c> — kullanıcı başına.
/// Asıl iş günlüğü servis ve Agent'ındır (<see cref="AgentLogFolder"/>); System Tray'deki "Günlük klasörünü aç" onu açar.
/// </summary>
public static class UiLog
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scadex", "RemoteDesk", "logs");

    /// <summary> Servis + Agent günlükleri (SYSTEM yazar, Users okur). </summary>
    public static string AgentLogFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Scadex", "RemoteDesk", "logs");

    public static void Configure(LoggerConfiguration config) => config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.File(
            Path.Combine(Folder, "ui-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

    /// <summary> Servis + Agent günlük klasörünü Gezgin'de açar (yoksa arayüzünkini). </summary>
    public static void OpenAgentLogFolder()
    {
        string folder = Directory.Exists(AgentLogFolder) ? AgentLogFolder : Folder;
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
    }
}
