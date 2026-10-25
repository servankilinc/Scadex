using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Contracts.Local;

/// <summary>
/// PC'deki Agent (SYSTEM, kullanıcı oturumunda) ↔ WPF arayüz (normal yetki) arasındaki yerel named pipe
/// <list type="bullet">
/// <item>Satır başına bir UTF-8 JSON <see cref="AgentMessage"/>; satır <see cref="MaxMessageBytes"/>'ı aşarsa bağlantı kapanır.</item>
/// <item>Pipe oturum başınadır (<see cref="PipeName"/>) ve yalnızca SYSTEM'e ve o oturumun kullanıcısına (logon SID) açıktır.</item>
/// <item>Arayüz → Agent YALNIZCA <see cref="Subscribe"/> ve üç zararsız komut (<see cref="Reconnect"/>, <see cref="ProbeEncoders"/>,
/// <see cref="SetPreferredEncoder"/>) gönderebilir. Girdi, yayın ve dosya komutu bu kanaldan GEÇMEZ — yerel bir kullanıcı SYSTEM yetkili
/// Agent'a iş yaptıramasın diye; bu listeyi genişletmeyin.</item>
/// <item>Agent → arayüz yalnızca <see cref="Snapshot"/> gönderir (değişince, en çok saniyede bir); sürüm uymazsa bir kez
/// <see cref="UnsupportedVersion"/>.</item>
/// </list>
/// Sürüm uymazsa Agent bağlantıyı kapatır, arayüz "sürüm uyuşmuyor" gösterir (servis ile arayüz aynı paketten kurulmalı).
/// <para/>
/// Yalnızca Windows istemcisinin KENDİ İÇİDİR (Agent ↔ arayüz); merkezle sözleşme değildir, sunucu bu ad alanını kullanmaz. Başka platformun
/// istemcisi kendi yerel düzenini kurar.
/// </summary>
public static class AgentPipeContract
{
    public const int Version = 1;

    public const int MaxMessageBytes = 64 * 1024;

    /// <summary> Named pipe'lar oturumlar arası ortaktır: ad oturum Id'sini taşır, ACL de yalnızca o oturumun kullanıcısını kabul eder. </summary>
    public static string PipeName(int sessionId) => $"Scadex.RemoteDesk.Agent.{sessionId}";

    // Arayüz → Agent
    public const string Subscribe = "subscribe";
    public const string Reconnect = "reconnect";
    public const string ProbeEncoders = "probeEncoders";
    public const string SetPreferredEncoder = "setPreferredEncoder";

    // Agent → arayüz
    public const string Snapshot = "snapshot";
    /// <summary> Arayüzün protokol sürümü Agent'ınkinden farklı; Agent bunu gönderip bağlantıyı kapatır (<c>V</c> = Agent'ın sürümü). </summary>
    public const string UnsupportedVersion = "unsupportedVersion";
}

/// <param name="V">Protokol sürümü (<see cref="AgentPipeContract.Version"/>).</param>
/// <param name="Snapshot">Yalnızca <see cref="AgentPipeContract.Snapshot"/>'ta.</param>
/// <param name="Encoder">Yalnızca <see cref="AgentPipeContract.SetPreferredEncoder"/>'da; <c>null</c> = otomatik seçime dön.</param>
public sealed record AgentMessage(int V, string Type, AgentSnapshot? Snapshot = null, EncoderKind? Encoder = null);

public enum AgentConnectionState
{
    /// <summary> <c>appsettings.json</c>'da merkez adresi ya da cihaz kimliği eksik. </summary>
    NotConfigured = 0,
    Connecting = 1,
    Connected = 2,
    /// <summary> Merkez cihaz kimliğini reddetti (401). </summary>
    Unauthorized = 3,
    AlreadyConnected = 4,
    NetworkNotAllowed = 5,
    Disconnected = 6,
}

public enum AgentStreamState
{
    Idle = 0,
    Probing = 1,
    Starting = 2,
    Streaming = 3,
    /// <summary> Ekran kilitli / masaüstü değişti — kilit açılınca kendiliğinden sürer. </summary>
    ScreenLocked = 4,
    Retrying = 5,
    Failed = 6,
}

/// <summary> Arayüzün gösterdiği her şey, tek parça. Sır, bilet ve yayın adresinin parolası BURADA YOKTUR. </summary>
/// <param name="DeviceId">Yapılandırmadaki cihaz kimliği (Basic Auth kullanıcı adı); boş olabilir.</param>
/// <param name="SessionUser">Agent'ın çalıştığı oturumun kullanıcısı (<c>DOMAIN\kullanıcı</c>).</param>
/// <param name="Controller">Uzaktan kontrol eden merkez kullanıcısı; <c>null</c> = kontrol yok.</param>
public sealed record AgentSnapshot(
    string AgentVersion,
    string MachineName,
    string SessionUser,
    string DeviceId,
    AgentConnectionView Connection,
    FfmpegView Ffmpeg,
    IReadOnlyList<AgentMonitorView> Monitors,
    EncoderPanelView Encoders,
    string? Controller,
    bool FileTransferActive);

public sealed record AgentConnectionView(AgentConnectionState State, string CentralUrl, string Detail, string? DeviceName, string? CabinetName);

public sealed record FfmpegView(string Path, bool Exists, string? Version, bool IsGpl);

public sealed record AgentMonitorView(MonitorInfo Monitor, StreamView Stream);

/// <param name="Target">Maskeli hedef (<c>rtsp://***@…</c>) — bilet içermez.</param>
public sealed record StreamView(
    AgentStreamState State,
    Guid? SessionId,
    EncoderView? Encoder,
    bool IsForced,
    VideoProfile? Profile,
    string? Target,
    DateTime? StartedAt,
    double? Fps,
    double? Speed,
    string? Bitrate,
    double? CpuPercent,
    long? MemoryMb,
    int Downgrades,
    int Restarts,
    string? Message);

public sealed record EncoderView(EncoderKind Kind, string DisplayName, VideoCodec Codec, string Chain, bool IsHardware, string FfmpegEncoder, bool CanScale);

/// <param name="Preferred">PC başına tercih (bellekte); <c>null</c> = otomatik.</param>
/// <param name="ProbeError">Son "tümünü sına" komutunun hatası (sabit metin).</param>
public sealed record EncoderPanelView(bool IsProbing, EncoderKind? Preferred, string? PreferredName, IReadOnlyList<MonitorProbeView> Probes, string? ProbeError);

public sealed record MonitorProbeView(
    MonitorInfo Monitor,
    DateTime ProbedAt,
    IReadOnlyList<ProbeResultView> Results,
    EncoderKind? SelectedKind,
    bool IsPreferenceApplied,
    bool PreferenceUnavailable);

public sealed record ProbeResultView(EncoderView Encoder, bool Ok, string? Reason, double DurationSeconds);
