using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary>
/// PC istemcisi ↔ merkez kontrol düzlemi. JSON Scadex'le aynıdır: camelCase, enum SAYI, <c>null</c> alanlar gövdede kalır. İstemci dışarı bağlanır; PC hiçbir port açmaz.
/// <para/>
/// Sözleşme işletim sisteminden BAĞIMSIZDIR: bugün tek istemci Windows'tur, ileride Linux/Android istemcisi aynı hub'a sunucu değişmeden
/// bağlanabilmelidir. Bu yüzden telde yalnızca platformdan bağımsız kavramlar taşınır (DOM tuş kodu, [0,1] koordinat, RTSP yayın adresi, opak
/// yol metni). Platforma özgü alanlar (<see cref="Media.MonitorInfo"/>'daki DXGI ayrıntıları, <see cref="Media.EncoderKind"/>) yalnızca
/// bilgi içindir; sunucu onları YORUMLAMAZ. Yeni alan eklerken bu ayrımı koruyun.
/// </summary>
public static class PcHubContract
{
    /// <summary> Hub yolu (sunucu eşler, istemci <c>CentralApiUrl</c>'e ekler). </summary>
    public const string Path = "/hubs/remote-desk/pc";

    // İstemci → sunucu metot adları
    public const string Hello = nameof(Hello);
    public const string ReportMonitors = nameof(ReportMonitors);
    public const string ReportStreamState = nameof(ReportStreamState);
    public const string ReportFileTransfer = nameof(ReportFileTransfer);
    public const string ReportControlInput = nameof(ReportControlInput);
}

/// <summary> Sunucu → istemci. Metot adları arayüz metot adlarıdır (<c>Hub&lt;IPcHubClient&gt;</c>). </summary>
public interface IPcHubClient
{
    Task StartScreenStream(StartScreenStreamCommand command);
    Task StopScreenStream(StopScreenStreamCommand command);

    // Uzaktan kontrol (Faz 8, RemoteDesk.md § 12). Girdi yalnızca ControlStarted ile ControlEnded arasında, o oturumun kimliğiyle kabul edilir.
    Task ControlStarted(ControlStartedCommand command);
    Task ControlEnded(ControlEndedCommand command);
    Task Input(InputBatch batch);

    // Dosya transferi. ListDirectory SignalR client-result ile değer döndürür (tipli proxy'de son parametre
    // CancellationToken: sunucu zaman aşımı için kullanır, PC'ye gönderilmez). Baytlar hub'dan GEÇMEZ (biletli HTTP).
    Task<DirectoryListing> ListDirectory(ListDirectoryCommand command, CancellationToken cancellationToken = default);
    Task UploadFile(FileUploadCommand command);
    Task DownloadFile(FileDownloadCommand command);

    // Panolar arası aktarım. GetClipboard client-result ile uzak panonun metnini döndürür (metin yoksa null);
    // son parametre CancellationToken sunucu zaman aşımı içindir, PC'ye gönderilmez. SetClipboard metni uzak panoya yazar.
    Task<string?> GetClipboard(CancellationToken cancellationToken = default);
    Task SetClipboard(SetClipboardCommand command);
}

/// <summary>
/// Bağlantının ilk ve zorunlu çağrısı; kabul edilene kadar başka çağrı yapılamaz. PC'nin KİM olduğu burada değil bağlantının kendisindedir:
/// hub Basic Auth ister (kullanıcı adı = cihaz Id'si, parola = diyagramda üretilen parola); yanlış/eksik kimlik401 alır
/// </summary>
public sealed record HelloRequest(
    string ClientVersion,
    string OsVersion,
    string MachineName,
    string UserName,
    MonitorInfo[] Monitors);

public enum HelloStatus
{
    /// <summary> Bağlantı kimliği doğrulanan cihaza kaydedildi. </summary>
    Accepted = 1,
    /// <summary> Bu cihaz başka bir bağlantıda — ilk bağlanan kazanır. </summary>
    AlreadyConnected = 4,
    /// <summary> Bağlantı <c>AllowedNetworks</c> dışından geldi. </summary>
    NetworkNotAllowed = 5
}

public sealed record HelloResponse(
    HelloStatus Status,
    Guid? DeviceId,
    string? DeviceName,
    string? CabinetName);

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
    /// <summary> Ekran kilitli / yakalanamıyor (Windows'ta güvenli masaüstü geçişi) — kendiliğinden sürer. </summary>
    ScreenLocked = 4,
    Failed = 5,
    Stopped = 6
}

/// <param name="FailureReason">Yalnızca sabit metinler (istemcinin <c>FailureExplainer</c>'ı); ham FFmpeg çıktısı ASLA.</param>
public sealed record StreamStateReport(Guid SessionId, int MonitorIndex, ScreenStreamState State, string? Encoder, string? FailureReason);
