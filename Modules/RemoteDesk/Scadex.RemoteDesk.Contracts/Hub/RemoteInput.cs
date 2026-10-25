namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary> Uzaktan kontrol olayının türü (RemoteDesk.md § 12.2). Sayı olarak taşınır; numaralar sabittir. </summary>
public enum InputEventType
{
    Move = 1,
    Down = 2,
    Up = 3,
    Wheel = 4,
    /// <summary> <c>Code</c> zorunlu ve <see cref="KeyboardCodes"/> kümesinde olmalı. </summary>
    KeyDown = 5,
    /// <summary> <c>Code</c> zorunlu ve <see cref="KeyboardCodes"/> kümesinde olmalı. </summary>
    KeyUp = 6
}

/// <summary> Fare düğmesi — DOM <c>MouseEvent.button</c> ile aynı numaralar. </summary>
public enum MouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2
}

/// <summary>
/// Tek bir girdi olayı. Tarayıcı üretir, sunucu doğrulayıp PC'ye aynen iletir.
/// </summary>
/// <param name="Seq">Tarayıcıdaki artan sıra numarası (kayıp/sıra kontrolü ve günlük için).</param>
/// <param name="T">Tarayıcıda olayın oluştuğu an (Unix ms).</param>
/// <param name="X">[0, 1] — izlenen monitörün genişliğine göre; <c>Move</c>'da zorunlu, <c>Down/Up</c>'ta verilirse önce oraya gidilir.</param>
/// <param name="Y">[0, 1] — izlenen monitörün yüksekliğine göre.</param>
/// <param name="Button"><c>Down/Up</c>'ta zorunlu.</param>
/// <param name="DeltaX">Yatay tekerlek; DOM yönü (pozitif = sağa), protokol birimi (120 = bir çentik — Windows'unkiyle aynı; başka platformun
/// istemcisi kendi birimine çevirir).</param>
/// <param name="DeltaY">Dikey tekerlek; DOM yönü (pozitif = aşağı/kullanıcıya doğru), protokol birimi (120 = bir çentik). İstemci işaretini ve
/// birimini kendi platformuna çevirir.</param>
/// <param name="Code"><c>KeyDown/KeyUp</c>'ta <c>KeyboardEvent.code</c> (ör. <c>KeyA</c>) — tuşun fiziksel konumu, karakter değil.
/// Tuşa basılı tutulunca tarayıcının tekrarladığı <c>KeyDown</c>'lar da gönderilir: PC enjekte edilen tuşu kendisi tekrarlamaz (Windows).</param>
public sealed record InputEvent(
    long Seq,
    long T,
    InputEventType Type,
    double? X = null,
    double? Y = null,
    MouseButton? Button = null,
    int? DeltaX = null,
    int? DeltaY = null,
    string? Code = null);

/// <summary>
/// Tarayıcının ~60 Hz'de gönderdiği paket. Aynı paket sunucudan PC'ye <c>IPcHubClient.Input</c> ile iletilir.
/// Hareket için son-durum semantiği: ardışık <c>Move</c>'lardan yalnızca sonuncusu işlenir; <c>Down/Up/Wheel/KeyDown/KeyUp</c> asla düşürülmez,
/// sıra korunur.
/// </summary>
public sealed record InputBatch(Guid ControlSessionId, int MonitorIndex, InputEvent[] Events);

/// <summary>
/// PC'deki girdinin durumu — operatöre "tıklamam neden çalışmıyor"un cevabı. İstemci yalnızca DEĞİŞİNCE
/// bildirir (<c>PcHubContract.ReportControlInput</c>); kontrol <see cref="Ok"/> varsayımıyla başlar. Platformdan bağımsızdır: "güvenli masaüstü"
/// Windows'ta kilit ekranı / UAC onayıdır, başka platform kendi karşılığını bildirir. Numaralar sabittir.
/// </summary>
public enum ControlInputState
{
    /// <summary> Girdi normal masaüstüne gidiyor. </summary>
    Ok = 1,
    /// <summary> Kilit ekranı / UAC onayı açık ve girdi ORAYA gidiyor (bilgi). </summary>
    SecureDesktop = 2,
    /// <summary> Kilit ekranı / UAC onayı açık ama istemci oraya erişemiyor — girdi ulaşmıyor. </summary>
    SecureDesktopUnavailable = 3,
    /// <summary> İşletim sistemi girdiyi reddetti; nedeni bilinmiyor (ör. bir güvenlik yazılımı). </summary>
    Rejected = 4,
    /// <summary> İzlenen monitör PC'de yok (çıkarıldı, düzen değişti). </summary>
    MonitorMissing = 5
}

/// <summary> PC → merkez: kontrol oturumunun girdi durumu değişti. Başka/bitmiş oturumun bildirimi sunucuda yok sayılır. </summary>
public sealed record ControlInputReport(Guid ControlSessionId, ControlInputState State);

/// <summary> PC'ye: uzaktan kontrol başladı — istemci göstergeyi açar, girdi kabul etmeye başlar. </summary>
public sealed record ControlStartedCommand(Guid ControlSessionId, string UserName);

/// <summary> PC'ye: uzaktan kontrol bitti — istemci basılı kalan düğme ve tuşları bırakır, göstergeyi kapatır. Bilinmeyen oturum için sessizce başarılıdır. </summary>
public sealed record ControlEndedCommand(Guid ControlSessionId);
