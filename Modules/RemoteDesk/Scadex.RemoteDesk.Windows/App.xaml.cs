using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Services;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Services.Monitors;
using Scadex.RemoteDesk.Windows.Services.Streaming;
using Scadex.RemoteDesk.Windows.ViewModels;
using Scadex.RemoteDesk.Windows.Views;
using System.Windows;

namespace Scadex.RemoteDesk.Windows;

public partial class App : Application
{
    /// <summary> Host durdurulurken beklenecek en uzun süre; aşılırsa süreç yine de kapanır. </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            // İçerik kökü exe'nin klasörüdür, çalışma klasörü değil: oturum açılışında Görev Zamanlayıcı ile
            // başlatılan süreçte çalışma klasörü System32 olur ve appsettings.json bulunamazdı.
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<RemoteDeskClientOptions>(hostContext.Configuration.GetSection(RemoteDeskClientOptions.Section));

                // Services
                services.AddSingleton<ChildProcessJob>();   // tek Job Object: uygulama ölünce tüm FFmpeg'ler ölür
                services.AddSingleton<IFfmpegLocator, FfmpegLocator>();
                services.AddSingleton<IMonitorService, MonitorService>();
                services.AddSingleton<IEncoderProbeService, EncoderProbeService>();
                services.AddSingleton<IScreenStreamService, ScreenStreamService>();

                // ViewModels
                services.AddSingleton<HomeVM>();
                services.AddSingleton<MainWindowVM>();

                // Views — yalnızca MainWindow. Diğer View'lar Templates/DataTemplate.xaml ile ViewModel'den oluşur.
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (AppHost is null) throw new InvalidOperationException("AppHost is not initialized.");

        await AppHost.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Burada await KULLANILMAZ: async void OnExit'te WPF ilk await'te kapanmaya devam eder ve süreç, hosted
        // service'ler (bağlantı, FFmpeg) durmadan biter. Eşzamanlı ve zaman aşımlı bekleniyor; Task.Run, StopAsync'in
        // devamlarının UI bağlamını beklemesini (kilitlenme) önler.
        if (AppHost is not null)
        {
            try
            {
                // Yayınlar önce nazikçe durur (FFmpeg'e 'q'); yetişmezse Job Object süreç kapanınca yine öldürür.
                Task.Run(async () =>
                {
                    await AppHost.Services.GetRequiredService<IScreenStreamService>().StopAllAsync();
                    await AppHost.StopAsync(CancellationToken.None);
                }).Wait(ShutdownTimeout);
            }
            catch (AggregateException ex)
            {
                AppHost.Services.GetService<ILogger<App>>()?.LogError(ex.InnerException ?? ex, "Kapanışta host durdurulamadı.");
            }

            AppHost.Dispose();
        }

        base.OnExit(e);
    }
}
