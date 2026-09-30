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

/// <summary>
/// Merkezin <c>StartScreenStream</c> komutundan gelen yayın hedefi. Yayın yalnızca merkezin isteğiyle başlar; sahadaki test yayını
/// 2026-09-30'da kaldırıldı.
/// </summary>
/// <param name="RtspUrl">Bilet parola alanında (<c>rtsp://pc:{bilet}@merkez:8554/pc_…</c>) — yalnızca maskeli gösterilir.</param>
/// <param name="SessionId">Merkezin yayın oturumu; komutların idempotency anahtarı.</param>
/// <param name="Profile">Merkezin verdiği profil; <c>null</c> = seçilen encoder'ın varsayılanı.</param>
public sealed record StreamTarget(string RtspUrl, Guid SessionId, VideoProfile? Profile = null)
{
    public string Describe() => "Merkez — " + FailureExplainer.Mask(RtspUrl);
}

/// <summary> Arayüzün gösterdiği anlık durum. Servis her değişimde yenisini yayınlar (değişmez kayıt). </summary>
public sealed record StreamStatus(int MonitorIndex, StreamState State)
{
    /// <summary> Merkezin yayın oturumu; monitörde yayın yokken (Idle) <c>null</c> olabilir. </summary>
    public Guid? SessionId { get; init; }
    public EncoderCandidate? Encoder { get; init; }
    /// <summary> Kodlayıcı otomatik seçilmedi: PC tercihinden geldi (bellekte, uygulama kapanınca otomatiğe döner). </summary>
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
