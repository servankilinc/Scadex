using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Encoding;

namespace Scadex.RemoteDesk.Windows.Services.Streaming;

public enum StreamState
{
    Idle = 0,
    Probing = 1,
    Starting = 2,
    Streaming = 3,
    /// <summary> Ekran kilitli / masaüstü değişti — kilit açılınca kendiliğinden sürer. </summary>
    ScreenLocked = 4,
    /// <summary> Beklenmedik çıkış; artan aralıkla yeniden deneniyor. </summary>
    Retrying = 5,
    Failed = 6,
}

public enum StreamTargetKind
{
    /// <summary> Yalnızca kodla ve at: sunucu gerekmez. Sahada kodlayıcı seçimini ve yükü görmek için. </summary>
    EncodeOnly = 1,
    /// <summary> Verilen RTSP adresine yayınla (erişilebilen bir MediaMTX). </summary>
    Rtsp = 2,
}

/// <summary> <c>{monitor}</c> yer tutucusu monitör sırasıyla değiştirilir. </summary>
public sealed record StreamTarget(StreamTargetKind Kind, string? RtspUrl = null)
{
    public string Describe(int monitorIndex) => Kind == StreamTargetKind.EncodeOnly
        ? "Yalnızca kodlama (ağa gönderilmiyor)"
        : FailureExplainer.Mask(ResolveUrl(monitorIndex));

    public string ResolveUrl(int monitorIndex) => (RtspUrl ?? "").Replace("{monitor}", monitorIndex.ToString());
}

/// <summary> Arayüzün gösterdiği anlık durum. Servis her değişimde yenisini yayınlar (değişmez kayıt). </summary>
public sealed record StreamStatus(int MonitorIndex, StreamState State)
{
    public EncoderCandidate? Encoder { get; init; }
    /// <summary> Kodlayıcı otomatik seçilmedi, sahada elle denendi. </summary>
    public bool IsForced { get; init; }
    public VideoProfile? Profile { get; init; }
    public string? Target { get; init; }
    public DateTime? StartedAt { get; init; }
    public double? Fps { get; init; }
    /// <summary> FFmpeg hızı (1 = gerçek zaman). &lt; 0,9 sürerse geri kalma bekçisi profili düşürür. </summary>
    public double? Speed { get; init; }
    public string? Bitrate { get; init; }
    public double? CpuPercent { get; init; }
    public long? MemoryMb { get; init; }
    public int Downgrades { get; init; }
    public int Restarts { get; init; }
    public string? Message { get; init; }
}
