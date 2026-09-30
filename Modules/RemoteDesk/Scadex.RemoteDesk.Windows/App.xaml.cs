using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Services;
using Scadex.RemoteDesk.Windows.Services.Connection;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Services.Monitors;
using Scadex.RemoteDesk.Windows.Services.Network;
using Scadex.RemoteDesk.Windows.Services.Shell;
using Scadex.RemoteDesk.Windows.Services.Streaming;
using Scadex.RemoteDesk.Windows.ViewModels;
using Scadex.RemoteDesk.Windows.Views;
using System.ComponentModel;
using System.Windows;

namespace Scadex.RemoteDesk.Windows;

public partial class App : Application
{
    /// <summary> Host durdurulurken beklenecek en uzun süre; aşılırsa süreç yine de kapanır. </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    // Tek örnek oturum başınadır (Local\): hızlı kullanıcı değiştirmede her oturumun kendi istemcisi olur (§ 9.1).
    private const string SingleInstanceName = @"Local\Scadex.RemoteDesk.Windows";
    private const string ShowSignalName = @"Local\Scadex.RemoteDesk.Windows.Show";

    /// <summary> Oturum açılışında başlatan görev bu argümanı verir: pencere açılmaz, uygulama System Tray'de başlar. </summary>
    private const string TrayArgument = "--tray";

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;
    private TrayIcon? _tray;
    private bool _exiting;

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
                services.AddSingleton<INetworkAdapterService, NetworkAdapterService>();

                // Merkez bağlantısı: tek örnek hem hosted service (döngü) hem ICentralConnection (durum, "yeniden bağlan").
                services.AddSingleton<CentralConnectionService>();
                services.AddSingleton<ICentralConnection>(sp => sp.GetRequiredService<CentralConnectionService>());
                services.AddHostedService(sp => sp.GetRequiredService<CentralConnectionService>());

                // ViewModels
                services.AddSingleton<ConnectionVM>();
                services.AddSingleton<EncoderPanelVM>();
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

        // İkinci örnek: çalışanın penceresini öne getir ve çık (host hiç başlatılmaz).
        _singleInstance = new Mutex(true, SingleInstanceName, out bool isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowSignalName).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, Timeout.Infinite, false);

        await AppHost.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        // Pencereyi kapatmak uygulamayı kapatmaz, System Tray'ye indirir; çıkış yalnızca System Tray menüsünden.
        mainWindow.Closing += OnMainWindowClosing;
        // Oturum kapanırken / Windows kapanırken pencere gizlenmeye çalışmasın, uygulama kapansın.
        SessionEnding += (_, _) => _exiting = true;

        var connection = AppHost.Services.GetRequiredService<ConnectionVM>();
        _tray = new TrayIcon(ShowMainWindow, () => AppHost.Services.GetRequiredService<ICentralConnection>().Reconnect(), ExitApplication);
        _tray.SetStatus(connection.ShortText);
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionVM.ShortText)) _tray.SetStatus(connection.ShortText);
            else if (args.PropertyName == nameof(ConnectionVM.IsWatching)) _tray.SetWatching(connection.IsWatching);
        };

        if (!e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase))
            mainWindow.Show();

        base.OnStartup(e);
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        ((Window)sender!).Hide();
    }

    private void ShowMainWindow()
    {
        if (AppHost is null) return;
        var window = AppHost.Services.GetRequiredService<MainWindow>();
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary> Tek çıkış yolu: System Tray menüsü. Host ve yayınlar <see cref="OnExit"/>'te durdurulur. </summary>
    private void ExitApplication()
    {
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showWait?.Unregister(null);
        _showSignal?.Dispose();

        // Burada await KULLANILMAZ: async void OnExit'te WPF ilk await'te kapanmaya devam eder ve süreç, hosted
        // service'ler (bağlantı, FFmpeg) durmadan biter. Eşzamanlı ve zaman aşımlı bekleniyor; Task.Run, StopAsync'in
        // devamlarının UI bağlamını beklemesini (kilitlenme) önler.
        if (AppHost is not null && _singleInstance is not null)
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
        }
        AppHost?.Dispose();

        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}
