using System.Collections.ObjectModel;
using System.Windows;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Agent;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekran — sahada kontrol: en üstte merkez bağlantısı (<see cref="ConnectionVM"/>), FFmpeg/donanım, encoder sınaması ve PC tercihi
/// (<see cref="EncoderPanelVM"/>) ve monitör kartları (merkezin yayınının salt okunur durumu). Arayüz hiçbir şeyi kendisi okumaz: her şey
/// Agent'ın pipe'tan gelen özetidir. Özet iş parçacığı havuzundan gelir, burada Dispatcher'a taşınır.
/// </summary>
public class HomeVM : BaseViewModel
{
    private readonly IAgentLink _agent;

    public HomeVM(IAgentLink agent, ConnectionVM connection, EncoderPanelVM encoder)
    {
        _agent = agent;
        Connection = connection;
        Encoder = encoder;

        _agent.Changed += () => Application.Current?.Dispatcher.BeginInvoke(Apply);
        Apply();
    }

    public ConnectionVM Connection { get; }

    /// <summary> Encoder sınaması + PC tercihi (monitörlerden ayrı kutucuk). </summary>
    public EncoderPanelVM Encoder { get; }

    private string _ffmpegText = "";
    public string FfmpegText { get => _ffmpegText; private set => SetProperty(ref _ffmpegText, value); }

    private string _ffmpegWarning = "";
    public string FfmpegWarning
    {
        get => _ffmpegWarning;
        private set { if (SetProperty(ref _ffmpegWarning, value)) OnPropertyChanged(nameof(HasFfmpegWarning)); }
    }
    public bool HasFfmpegWarning => FfmpegWarning.Length > 0;

    public ObservableCollection<MonitorVM> Monitors { get; } = [];

    private string _machineText = "";
    public string MachineText { get => _machineText; private set => SetProperty(ref _machineText, value); }

    private void Apply()
    {
        var snapshot = _agent.State == AgentLinkState.Connected ? _agent.Snapshot : null;
        Connection.Apply(_agent.State, snapshot);

        if (snapshot is null)
        {
            Monitors.Clear();
            MachineText = "Agent'a bağlı değil — monitör ve yayın bilgisi yok.";
            FfmpegText = FfmpegWarning = "";
            Encoder.Apply(null, []);
            return;
        }

        FfmpegView ffmpeg = snapshot.Ffmpeg;
        FfmpegText = ffmpeg.Exists ? $"FFmpeg {ffmpeg.Version} — {(ffmpeg.IsGpl ? "GPL" : "LGPL")} · {ffmpeg.Path}" : $"FFmpeg bulunamadı: {ffmpeg.Path}";
        FfmpegWarning = !ffmpeg.Exists
            ? "FFmpeg yok: yayın ve sınama çalışmaz. tools\\ffmpeg\\ffmpeg.exe servis klasörüne konmalı."
            : ffmpeg.IsGpl ? "Bu bir GPL build'i — istemciyle müşteriye LGPL build dağıtılmalı (RemoteDesk.md § 7.4)." : "";

        ApplyMonitors(snapshot.Monitors);
        MachineText = Monitors.Count == 0
            ? "Masaüstüne bağlı monitör bulunamadı."
            : "Ekran kartları: " + string.Join(" + ", Monitors.Select(m => m.Monitor.GpuName).Distinct());
        Encoder.Apply(snapshot.Encoders, [.. snapshot.Monitors.Select(m => m.Monitor)]);
    }

    /// <summary> Monitör kümesi değişmediyse kartlar yerinde güncellenir (saniyede bir gelen yayın ilerlemesi kartları yeniden kurmasın). </summary>
    private void ApplyMonitors(IReadOnlyList<AgentMonitorView> monitors)
    {
        bool sameSet = Monitors.Count == monitors.Count && Monitors.Zip(monitors).All(p =>
            p.First.Monitor.Index == p.Second.Monitor.Index && p.First.Monitor.DeviceName == p.Second.Monitor.DeviceName
            && p.First.Monitor.Width == p.Second.Monitor.Width && p.First.Monitor.Height == p.Second.Monitor.Height
            && p.First.Monitor.Left == p.Second.Monitor.Left && p.First.Monitor.Top == p.Second.Monitor.Top);

        if (sameSet)
        {
            for (int i = 0; i < monitors.Count; i++)
                Monitors[i].Apply(monitors[i].Stream);
            return;
        }

        Monitors.Clear();
        foreach (var m in monitors)
            Monitors.Add(new MonitorVM(m.Monitor, m.Stream));
    }
}
