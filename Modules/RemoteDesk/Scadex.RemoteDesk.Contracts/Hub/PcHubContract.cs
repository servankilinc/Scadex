using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary>
/// Windows istemcisi ↔ merkez kontrol düzlemi. JSON Scadex'le aynıdır: camelCase, enum SAYI, <c>null</c> alanlar gövdede kalır. İstemci dışarı bağlanır; PC hiçbir port açmaz.
/// </summary>
public static class PcHubContract
{
    /// <summary> Hub yolu (sunucu eşler, istemci <c>CentralApiUrl</c>'e ekler). </summary>
    public const string Path = "/hubs/remote-desk/pc";

    // İstemci → sunucu metot adları
    public const string Hello = nameof(Hello);
    public const string ReportMonitors = nameof(ReportMonitors);
    public const string ReportStreamState = nameof(ReportStreamState);
}

/// <summary> Sunucu → istemci. Metot adları arayüz metot adlarıdır (<c>Hub&lt;IPcHubClient&gt;</c>). </summary>
public interface IPcHubClient
{
    Task StartScreenStream(StartScreenStreamCommand command);
    Task StopScreenStream(StopScreenStreamCommand command);
}

/// <summary> Bağlantının ilk ve zorunlu çağrısı; kabul edilene kadar başka çağrı yapılamaz. </summary>
/// <param name="MacAddresses">Fiziksel ağ kartlarının MAC'leri (biçim serbest; sunucu <see cref="MacAddress.Normalize"/> ile karşılaştırır).</param>
public sealed record HelloRequest(
    string[] MacAddresses,
    string ClientVersion,
    string OsVersion,
    string MachineName,
    string UserName,
    MonitorInfo[] Monitors);

public enum HelloStatus
{
    /// <summary> Tam bir PC cihazıyla eşleşti; bağlantı o cihaza kaydedildi. </summary>
    Accepted = 1,
    /// <summary> Hiçbir aktif PC cihazının MAC'i eşleşmedi — teknisyen MAC'i diyagrama girmeli. </summary>
    UnknownDevice = 2,
    /// <summary> MAC'ler birden fazla PC cihazıyla eşleşti (iki kart iki cihaza yazılmış). </summary>
    Ambiguous = 3,
    /// <summary> Bu cihaz başka bir bağlantıda — ilk bağlanan kazanır. </summary>
    AlreadyConnected = 4,
    /// <summary> Bağlantı <c>AllowedNetworks</c> dışından geldi. </summary>
    NetworkNotAllowed = 5
}

/// <param name="MatchedMacAddresses">Eşleşen cihaz(lar)ın MAC'leri — <see cref="HelloStatus.Ambiguous"/>'ta hangi kartların çakıştığını gösterir.</param>
public sealed record HelloResponse(
    HelloStatus Status,
    Guid? DeviceId,
    string? DeviceName,
    string? CabinetName,
    string[] MatchedMacAddresses);

/// <summary> Monitörün yayınını başlat. <c>SessionId</c> idempotency anahtarıdır: aynı oturum tekrar gelirse yeni FFmpeg açılmaz. </summary>
/// <param name="PublishUrl">Bilet parola alanında, tam RTSP adresi (<c>rtsp://pc:{bilet}@merkez:8554/pc_…</c>). Loglanmaz.</param>
/// <param name="Profile"><c>null</c> = istemci seçilen kodlayıcının varsayılanını kullanır.</param>
public sealed record StartScreenStreamCommand(Guid SessionId, int MonitorIndex, string PublishUrl, VideoProfile? Profile);

/// <summary> Bilinmeyen/eski oturum için sessizce başarılıdır. </summary>
public sealed record StopScreenStreamCommand(Guid SessionId);

public enum ScreenStreamState
{
    Starting = 1,
    Streaming = 2,
    /// <summary> FFmpeg düştü, yeniden deneniyor. </summary>
    Retrying = 3,
    /// <summary> Ekran kilitli / güvenli masaüstü — kilit açılınca kendiliğinden sürer. </summary>
    ScreenLocked = 4,
    Failed = 5,
    Stopped = 6
}

/// <param name="FailureReason">Yalnızca sabit metinler (istemcinin <c>FailureExplainer</c>'ı); ham FFmpeg çıktısı ASLA.</param>
public sealed record StreamStateReport(Guid SessionId, int MonitorIndex, ScreenStreamState State, string? Encoder, string? FailureReason);
