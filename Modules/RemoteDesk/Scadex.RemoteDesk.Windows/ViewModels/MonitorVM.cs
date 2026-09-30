using System.Windows.Media;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Tek monitörün kartı: donanım bilgisi ve merkezin başlattığı yayının canlı durumu (SALT OKUNUR — yayın yalnızca merkezin isteğiyle
/// başlar/durur; encoder sınaması ve tercihi <see cref="EncoderPanelVM"/>'dedir). Servis olayları iş parçacığı havuzundan gelir;
/// <see cref="HomeVM"/> onları Dispatcher'a taşıyıp <see cref="Apply"/>'ı çağırır.
/// </summary>
public sealed class MonitorVM : BaseViewModel
{
    private static readonly Brush Ok = Freeze(Color.FromRgb(0x2E, 0x9E, 0x5B));
    private static readonly Brush Bad = Freeze(Color.FromRgb(0xD1, 0x43, 0x43));
    private static readonly Brush Warn = Freeze(Color.FromRgb(0xC7, 0x8A, 0x10));
    private static readonly Brush Muted = Freeze(Color.FromRgb(0x80, 0x88, 0x94));
    private static readonly Brush Info = Freeze(Color.FromRgb(0x2F, 0x6F, 0xC9));

    public MonitorVM(MonitorInfo monitor, StreamStatus status)
    {
        Monitor = monitor;
        Apply(status);
    }

    public MonitorInfo Monitor { get; }

    public string Title => $"Monitör {Monitor.Index}{(Monitor.IsPrimary ? " (birincil)" : "")} — {Monitor.DeviceName.Replace(@"\\.\", "")}";
    public string Geometry => $"{Monitor.Width}×{Monitor.Height} · konum ({Monitor.Left}, {Monitor.Top})";
    public string Gpu => $"{Monitor.GpuName} · {VendorText(Monitor.GpuVendor)} · adaptör {Monitor.AdapterIndex}, çıkış {Monitor.OutputIndex}";

    private string _stateText = "";
    public string StateText { get => _stateText; private set => SetProperty(ref _stateText, value); }

    private Brush _stateBrush = Muted;
    public Brush StateBrush { get => _stateBrush; private set => SetProperty(ref _stateBrush, value); }

    private string _encoderText = "—";
    public string EncoderText { get => _encoderText; private set => SetProperty(ref _encoderText, value); }

    private string _profileText = "—";
    public string ProfileText { get => _profileText; private set => SetProperty(ref _profileText, value); }

    private string _liveText = "—";
    public string LiveText { get => _liveText; private set => SetProperty(ref _liveText, value); }

    private Brush _liveBrush = Muted;
    public Brush LiveBrush { get => _liveBrush; private set => SetProperty(ref _liveBrush, value); }

    private string _targetText = "—";
    public string TargetText { get => _targetText; private set => SetProperty(ref _targetText, value); }

    private string _messageText = "";
    public string MessageText { get => _messageText; private set => SetProperty(ref _messageText, value); }

    public void Apply(StreamStatus s)
    {
        bool isActive = s.State is not (StreamState.Idle or StreamState.Failed);
        (StateText, StateBrush) = s.State switch
        {
            StreamState.Idle => ("Yayın yok — merkez izlemek isteyince başlar", Muted),
            StreamState.Probing => ("Encoder'lar sınanıyor…", Info),
            StreamState.Starting => ("Başlıyor…", Info),
            StreamState.Streaming => ("● Yayında", Ok),
            StreamState.ScreenLocked => ("Ekran kilitli — kilit açılınca sürecek", Warn),
            StreamState.Retrying => ("Yeniden deneniyor", Warn),
            StreamState.Failed => ("Başarısız", Bad),
            _ => (s.State.ToString(), Muted),
        };

        if (s.State == StreamState.Idle)
        {
            // Yayın bitti: önceki yayının bilgileri ekranda kalmasın.
            EncoderText = ProfileText = TargetText = "—";
        }
        else
        {
            if (s.Encoder is { } e)
                EncoderText = $"{e.DisplayName} · {CodecText(e.Codec)} · {e.Chain} · {(e.IsHardware ? "donanım" : "yazılım")} (ffmpeg: {e.FfmpegEncoder})" +
                              (s.IsForced ? " — PC TERCİHİ (otomatik seçim değil)" : " — otomatik seçim");
            if (s.Profile is { } p)
                ProfileText = $"{p}{(s.Encoder is { CanScale: false } ? " (bu zincir ölçeklemez; kaynak çözünürlük)" : "")}" +
                              (s.Downgrades > 0 ? $" — yetişemediği için {s.Downgrades} kez düşürüldü" : "");
            if (s.Target is not null) TargetText = s.Target;
        }

        if (s.State == StreamState.Streaming)
        {
            // FFmpeg -progress, RTSP çıkışında bit hızını "N/A" verir; hedef bit hızı profilde görünür.
            string bitrate = s.Bitrate is { } b && b != "N/A" ? $" · {b}" : "";
            LiveText = $"{s.Fps:0.#} fps · hız {s.Speed:0.00}x{bitrate} · CPU %{s.CpuPercent:0.#} · {s.MemoryMb} MB" +
                       (s.Restarts > 0 ? $" · {s.Restarts} yeniden başlatma" : "") +
                       (s.StartedAt is { } at ? $" · {DateTime.Now - at:hh\\:mm\\:ss}" : "");
            LiveBrush = s.Speed is < 0.9 ? Warn : Ok;
        }
        else if (!isActive)
        {
            LiveText = "—";
            LiveBrush = Muted;
        }
        MessageText = s.Message ?? "";
    }

    private static string CodecText(VideoCodec c) => c switch { VideoCodec.H264 => "H.264", VideoCodec.Vp9 => "VP9", _ => c.ToString() };
    private static string VendorText(GpuVendor v) => v switch
    {
        GpuVendor.Nvidia => "NVIDIA", GpuVendor.Intel => "Intel", GpuVendor.Amd => "AMD", _ => "bilinmeyen üretici",
    };
    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
