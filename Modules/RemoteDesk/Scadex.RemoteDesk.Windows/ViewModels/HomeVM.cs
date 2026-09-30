using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Services.Monitors;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekran — sahada kontrol: en üstte merkez bağlantısı (<see cref="ConnectionVM"/>), FFmpeg/donanım, encoder sınaması ve PC tercihi
/// (<see cref="EncoderPanelVM"/>, monitörlerden ayrı) ve monitör kartları (merkezin yayınının salt okunur durumu).
/// </summary>
public class HomeVM : BaseViewModel
{
    private readonly IMonitorService _monitors;
    private readonly IScreenStreamService _streams;

    public HomeVM(IMonitorService monitors, IScreenStreamService streams, IFfmpegLocator ffmpeg, ConnectionVM connection, EncoderPanelVM encoder)
    {
        Connection = connection;
        Encoder = encoder;
        _monitors = monitors;
        _streams = streams;

        FfmpegInfo info = ffmpeg.Info;
        FfmpegText = info.Exists ? $"FFmpeg {info.Version} — {(info.IsGpl ? "GPL" : "LGPL")} · {info.Path}" : $"FFmpeg bulunamadı: {info.Path}";
        FfmpegWarning = !info.Exists
            ? "FFmpeg yok: yayın ve sınama çalışmaz. tools\\ffmpeg\\ffmpeg.exe uygulama klasörüne konmalı."
            : info.IsGpl ? "Bu bir GPL build'i — istemciyle müşteriye LGPL build dağıtılmalı (RemoteDesk.md § 7.4)." : "";

        RefreshCommand = new RelayCommand(_ => LoadMonitors());

        // Servis olayları iş parçacığı havuzundan gelir; kartlar yalnızca UI iş parçacığında güncellenir.
        _streams.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() =>
            Monitors.FirstOrDefault(m => m.Monitor.Index == status.MonitorIndex)?.Apply(status));
        // Monitör takılıp çıkarılınca / çözünürlük değişince liste yeniden okunur (zamanlayıcı yok — olayla).
        SystemEvents.DisplaySettingsChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(LoadMonitors);

        LoadMonitors();
    }

    public ConnectionVM Connection { get; }

    /// <summary> Encoder sınaması + PC tercihi (monitörlerden ayrı kutucuk). </summary>
    public EncoderPanelVM Encoder { get; }

    public string FfmpegText { get; }
    public string FfmpegWarning { get; }
    public bool HasFfmpegWarning => FfmpegWarning.Length > 0;

    public ObservableCollection<MonitorVM> Monitors { get; } = [];
    public ICommand RefreshCommand { get; }

    private string _machineText = "";
    public string MachineText { get => _machineText; private set => SetProperty(ref _machineText, value); }

    private void LoadMonitors()
    {
        Monitors.Clear();
        try
        {
            foreach (var monitor in _monitors.GetMonitors())
                Monitors.Add(new MonitorVM(monitor, _streams.GetStatus(monitor.Index)));
            MachineText = Monitors.Count == 0
                ? "Masaüstüne bağlı monitör bulunamadı."
                : "Ekran kartları: " + string.Join(" + ", Monitors.Select(m => m.Monitor.GpuName).Distinct());
        }
        catch (Exception ex)
        {
            MachineText = $"Monitörler okunamadı: {ex.Message}";
        }
        Encoder.Refresh();
    }
}
