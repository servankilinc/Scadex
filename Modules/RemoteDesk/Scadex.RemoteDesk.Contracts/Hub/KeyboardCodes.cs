namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary>
/// Uzaktan klavyede tel üzerinde kabul edilen tuşlar: tarayıcının <c>KeyboardEvent.code</c> değerleri. İşletim sisteminden BAĞIMSIZDIR — <c>code</c> fiziksel tuşun konumudur,
/// karakter değil; PC tuşu kendi klavye düzeniyle yorumlar (Türkçe Q'da <c>BracketLeft</c> = ğ). Sunucu yalnızca bu kümedeki kodları iletir (<see cref="IsKnown"/>); 
/// her PC istemcisi kodu kendi platformunun tuş kimliğine çevirir (Windows: set-1 scan code, <c>Scadex.RemoteDesk.Windows.Service</c> içindeki <c>WindowsScanCodes</c>; Linux: evdev;
/// Android: <c>KeyEvent</c>). Bir istemci kümedeki her kodu karşılamalıdır; küme genişlerse istemci tabloları da genişletilir.
/// Bilerek yok: <c>Pause</c>, medya/tarayıcı tuşları, F13+.
/// </summary>
public static class KeyboardCodes
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "KeyQ", "KeyW", "KeyE", "KeyR", "KeyT", "KeyY", "KeyU", "KeyI", "KeyO", "KeyP", "KeyA", "KeyS", "KeyD", "KeyF", "KeyG",
        "KeyH", "KeyJ", "KeyK", "KeyL", "KeyZ", "KeyX", "KeyC", "KeyV", "KeyB", "KeyN", "KeyM", "Digit1", "Digit2", "Digit3",
        "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9", "Digit0", "Minus", "Equal", "BracketLeft", "BracketRight",
        "Semicolon", "Quote", "Backquote", "Backslash", "Comma", "Period", "Slash", "IntlBackslash", "IntlRo", "IntlYen",
        "Escape", "Backspace", "Tab", "Enter", "Space", "CapsLock", "ShiftLeft", "ShiftRight", "ControlLeft", "ControlRight",
        "AltLeft", "AltRight", "MetaLeft", "MetaRight", "ContextMenu", "PrintScreen", "ScrollLock", "F1", "F2", "F3", "F4", "F5",
        "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "ArrowUp",
        "ArrowDown", "ArrowLeft", "ArrowRight", "NumLock", "NumpadDivide", "NumpadMultiply", "NumpadSubtract", "NumpadAdd",
        "NumpadEnter", "NumpadDecimal", "NumpadEqual", "Numpad0", "Numpad1", "Numpad2", "Numpad3", "Numpad4", "Numpad5",
        "Numpad6", "Numpad7", "Numpad8", "Numpad9",
    };

    /// <summary> Kod tel üzerinde kabul ediliyor mu (sunucu yalnızca bunları iletir). </summary>
    public static bool IsKnown(string? code) => code is not null && Known.Contains(code);
}
