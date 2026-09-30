using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services;
using Scadex.RemoteDesk.Windows.Services.Encoding;
using Scadex.RemoteDesk.Windows.Services.Ffmpeg;
using Scadex.RemoteDesk.Windows.Services.Monitors;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekran — sahada kontrol: bu PC'nin monitörleri, hangi kodlayıcının neden seçildiği ve test yayınının hangi
/// profille aktığı; en üstte merkez bağlantısı <see cref="ConnectionVM"/>.
/// </summary>
public class HomeVM : BaseViewModel
{
    private readonly IMonitorService _monitors;
    private readonly IEncoderProbeService _probes;
    private readonly IScreenStreamService _streams;

    public HomeVM(IMonitorService monitors, IEncoderProbeService probes, IScreenStreamService streams, IFfmpegLocator ffmpeg,
        IOptions<RemoteDeskClientOptions> options, ConnectionVM connection)
    {
        Connection = connection;
        _monitors = monitors;
        _probes = probes;
        _streams = streams;

        FfmpegInfo info = ffmpeg.Info;
        FfmpegText = info.Exists ? $"FFmpeg {info.Version} — {(info.IsGpl ? "GPL" : "LGPL")} · {info.Path}" : $"FFmpeg bulunamadı: {info.Path}";
        FfmpegWarning = !info.Exists
            ? "FFmpeg yok: yayın ve sınama çalışmaz. tools\\ffmpeg\\ffmpeg.exe uygulama klasörüne konmalı."
            : info.IsGpl ? "Bu bir GPL build'i — istemciyle müşteriye LGPL build dağıtılmalı (RemoteDesk.md § 7.4)." : "";

        _rtspUrl = options.Value.TestPublishUrl;
        RefreshCommand = new RelayCommand(_ => LoadMonitors());

        // Servis olayları iş parçacığı havuzundan gelir; kartlar yalnızca UI iş parçacığında güncellenir.
        _streams.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() =>
            Monitors.FirstOrDefault(m => m.Monitor.Index == status.MonitorIndex)?.Apply(status));
        // Monitör takılıp çıkarılınca / çözünürlük değişince liste yeniden okunur (zamanlayıcı yok — olayla).
        SystemEvents.DisplaySettingsChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(LoadMonitors);

        LoadMonitors();
    }

    public ConnectionVM Connection { get; }

    public string FfmpegText { get; }
    public string FfmpegWarning { get; }
    public bool HasFfmpegWarning => FfmpegWarning.Length > 0;

    public ObservableCollection<MonitorVM> Monitors { get; } = [];
    public ICommand RefreshCommand { get; }

    private string _machineText = "";
    public string MachineText { get => _machineText; private set => SetProperty(ref _machineText, value); }

    // ---------------------------------------------------------------- test yayını hedefi

    private bool _encodeOnly = true;
    /// <summary> Varsayılan: yalnızca kodla — sahada sunucu olmadan kodlayıcı seçimini ve yükü görmek için yeterli. </summary>
    public bool EncodeOnly { get => _encodeOnly; set { if (SetProperty(ref _encodeOnly, value)) OnPropertyChanged(nameof(ToRtsp)); } }
    public bool ToRtsp { get => !_encodeOnly; set => EncodeOnly = !value; }

    private string _rtspUrl;
    public string RtspUrl { get => _rtspUrl; set => SetProperty(ref _rtspUrl, value); }

    private StreamTarget CurrentTarget() => EncodeOnly
        ? new StreamTarget(StreamTargetKind.EncodeOnly)
        : new StreamTarget(StreamTargetKind.Rtsp, RtspUrl);

    private void LoadMonitors()
    {
        Monitors.Clear();
        try
        {
            foreach (var monitor in _monitors.GetMonitors())
                Monitors.Add(new MonitorVM(monitor, _probes, _streams, CurrentTarget));
            MachineText = Monitors.Count == 0
                ? "Masaüstüne bağlı monitör bulunamadı."
                : "Ekran kartları: " + string.Join(" + ", Monitors.Select(m => m.Monitor.GpuName).Distinct());
        }
        catch (Exception ex)
        {
            MachineText = $"Monitörler okunamadı: {ex.Message}";
        }
    }
}
