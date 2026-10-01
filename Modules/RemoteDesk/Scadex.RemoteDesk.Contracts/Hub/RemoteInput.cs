namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary> Uzaktan kontrol olayının türü (RemoteDesk.md § 12.2). Sayı olarak taşınır; numaralar sabittir. </summary>
public enum InputEventType
{
    Move = 1,
    Down = 2,
    Up = 3,
    Wheel = 4,
    /// <summary> Faz 9 — Faz 8'de sunucu iletmez. </summary>
    KeyDown = 5,
    /// <summary> Faz 9 — Faz 8'de sunucu iletmez. </summary>
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
/// <param name="DeltaX">Yatay tekerlek; DOM yönü (pozitif = sağa), Windows birimi (120 = bir çentik).</param>
/// <param name="DeltaY">Dikey tekerlek; DOM yönü (pozitif = aşağı/kullanıcıya doğru), Windows birimi (120 = bir çentik). İstemci işaretini çevirir.</param>
/// <param name="Code">Faz 9: <c>KeyboardEvent.code</c> (ör. <c>KeyA</c>).</param>
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
/// Hareket için son-durum semantiği: ardışık <c>Move</c>'lardan yalnızca sonuncusu işlenir; <c>Down/Up/Wheel</c> asla düşürülmez, sıra korunur.
/// </summary>
public sealed record InputBatch(Guid ControlSessionId, int MonitorIndex, InputEvent[] Events);

/// <summary> PC'ye: uzaktan kontrol başladı — istemci göstergeyi açar, girdi kabul etmeye başlar. </summary>
public sealed record ControlStartedCommand(Guid ControlSessionId, string UserName);

/// <summary> PC'ye: uzaktan kontrol bitti — istemci basılı kalan düğmeleri bırakır, göstergeyi kapatır. Bilinmeyen oturum için sessizce başarılıdır. </summary>
public sealed record ControlEndedCommand(Guid ControlSessionId);
