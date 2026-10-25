using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Services.Agent;
using Scadex.RemoteDesk.Windows.Services.Shell;
using Scadex.RemoteDesk.Windows.ViewModels;
using Scadex.RemoteDesk.Windows.Views;
using Serilog;
using System.ComponentModel;
using System.Windows;

namespace Scadex.RemoteDesk.Windows;

/// <summary>
/// Yalnızca arayüz: normal yetkiyle, oturum açılışında <c>HKLM\…\Run</c>'dan <c>--tray</c> ile başlar. Merkez bağlantısı,
/// yakalama, girdi, dosya ve pano Agent'tadır (servisin kullanıcı oturumunda başlattığı SYSTEM süreci); arayüz yalnızca onun durumunu
/// gösterir ve üç zararsız komut gönderir. Arayüzü kapatmak izlemeyi DURDURMAZ.
/// </summary>
public partial class App : Application
{
    /// <summary> Host durdurulurken beklenecek en uzun süre; aşılırsa süreç yine de kapanır. </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(3);

    // Tek örnek oturum başınadır (Local\): hızlı kullanıcı değiştirmede her oturumun kendi arayüzü olur.
    private const string SingleInstanceName = @"Local\Scadex.RemoteDesk.Windows";
    private const string ShowSignalName = @"Local\Scadex.RemoteDesk.Windows.Show";

    /// <summary> Oturum açılışında Run anahtarı bu argümanı verir: pencere açılmaz, arayüz System Tray'de başlar. </summary>
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
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog((_, config) => UiLog.Configure(config))
            .ConfigureServices(services =>
            {
                // Agent bağlantısı: tek örnek hem hosted service (döngü) hem IAgentLink (durum, komutlar).
                services.AddSingleton<AgentPipeClient>();
                services.AddSingleton<IAgentLink>(sp => sp.GetRequiredService<AgentPipeClient>());
                services.AddHostedService(sp => sp.GetRequiredService<AgentPipeClient>());

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

        var log = AppHost.Services.GetRequiredService<ILogger<App>>();
        log.LogInformation("Arayüz başladı: sürüm {Version}, {Mode}, günlük {Folder}",
            typeof(App).Assembly.GetName().Version,
            e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase) ? "System Tray'de" : "pencereli",
            UiLog.Folder);

        // Beklenmeyen hatalar yalnızca günlüğe yazılır; davranış değişmez (işlenmeyen hata yine uygulamayı kapatır).
        DispatcherUnhandledException += (_, args) => log.LogCritical(args.Exception, "Arayüzde işlenmeyen hata");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.LogCritical(args.ExceptionObject as Exception, "İşlenmeyen hata, arayüz kapanıyor");
        TaskScheduler.UnobservedTaskException += (_, args) => log.LogError(args.Exception, "Gözlenmeyen görev hatası");

        await AppHost.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        // Pencereyi kapatmak arayüzü kapatmaz, System Tray'ye indirir.
        mainWindow.Closing += OnMainWindowClosing;
        // Oturum kapanırken / Windows kapanırken pencere gizlenmeye çalışmasın, arayüz kapansın.
        SessionEnding += (_, _) => _exiting = true;

        var connection = AppHost.Services.GetRequiredService<ConnectionVM>();
        var agent = AppHost.Services.GetRequiredService<IAgentLink>();
        _tray = new TrayIcon(ShowMainWindow, agent.Reconnect, CloseUi);
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

    /// <summary> Yalnızca arayüz kapanır; Agent (izleme, uzaktan kontrol) servisin yönetiminde sürer. </summary>
    private void CloseUi()
    {
        AppHost?.Services.GetService<ILogger<App>>()?.LogInformation("Kullanıcı arayüzü System Tray'den kapattı (izleme sürer)");
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showWait?.Unregister(null);
        _showSignal?.Dispose();

        // Burada await KULLANILMAZ: async void OnExit'te WPF ilk await'te kapanmaya devam ederdi. Task.Run, StopAsync'in
        // devamlarının UI bağlamını beklemesini (kilitlenme) önler.
        if (AppHost is not null && _singleInstance is not null)
        {
            try { Task.Run(() => AppHost.StopAsync(CancellationToken.None)).Wait(ShutdownTimeout); }
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
