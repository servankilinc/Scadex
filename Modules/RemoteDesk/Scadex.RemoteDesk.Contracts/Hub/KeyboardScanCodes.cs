namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary>
/// Tarayıcının <c>KeyboardEvent.code</c> değeri → Windows scan code'u (set 1), RemoteDesk.md § 12.4. Değerler Chromium'un
/// <c>dom_code_data.inc</c> tablosuyla aynıdır. Yüksek bayt <c>0xE0</c> = genişletilmiş tuş (<c>KEYEVENTF_EXTENDEDKEY</c>).
/// <para/>
/// <c>code</c> fiziksel TUŞU söyler, karakteri değil: PC tuşu kendi klavye düzeniyle yorumlar (Türkçe Q'da <c>BracketLeft</c> = ğ,
/// <c>Quote</c> = i, <c>IntlBackslash</c> = &lt;). Sunucu yalnızca bu tablodaki kodları iletir; PC aynı tabloyla basar.
/// Bilerek yok: <c>Pause</c> (E1 dizisi, tek scan code değil), medya/tarayıcı tuşları, F13+.
/// </summary>
public static class KeyboardScanCodes
{
    private static readonly Dictionary<string, ushort> Map = new(StringComparer.Ordinal)
    {
        // Harfler
        ["KeyQ"] = 0x10, ["KeyW"] = 0x11, ["KeyE"] = 0x12, ["KeyR"] = 0x13, ["KeyT"] = 0x14, ["KeyY"] = 0x15, ["KeyU"] = 0x16,
        ["KeyI"] = 0x17, ["KeyO"] = 0x18, ["KeyP"] = 0x19,
        ["KeyA"] = 0x1E, ["KeyS"] = 0x1F, ["KeyD"] = 0x20, ["KeyF"] = 0x21, ["KeyG"] = 0x22, ["KeyH"] = 0x23, ["KeyJ"] = 0x24,
        ["KeyK"] = 0x25, ["KeyL"] = 0x26,
        ["KeyZ"] = 0x2C, ["KeyX"] = 0x2D, ["KeyC"] = 0x2E, ["KeyV"] = 0x2F, ["KeyB"] = 0x30, ["KeyN"] = 0x31, ["KeyM"] = 0x32,

        // Rakamlar (üst sıra)
        ["Digit1"] = 0x02, ["Digit2"] = 0x03, ["Digit3"] = 0x04, ["Digit4"] = 0x05, ["Digit5"] = 0x06,
        ["Digit6"] = 0x07, ["Digit7"] = 0x08, ["Digit8"] = 0x09, ["Digit9"] = 0x0A, ["Digit0"] = 0x0B,

        // Noktalama / düzene göre değişen tuşlar
        ["Minus"] = 0x0C, ["Equal"] = 0x0D, ["BracketLeft"] = 0x1A, ["BracketRight"] = 0x1B, ["Semicolon"] = 0x27, ["Quote"] = 0x28,
        ["Backquote"] = 0x29, ["Backslash"] = 0x2B, ["Comma"] = 0x33, ["Period"] = 0x34, ["Slash"] = 0x35,
        ["IntlBackslash"] = 0x56, ["IntlRo"] = 0x73, ["IntlYen"] = 0x7D,

        // Denetim
        ["Escape"] = 0x01, ["Backspace"] = 0x0E, ["Tab"] = 0x0F, ["Enter"] = 0x1C, ["Space"] = 0x39, ["CapsLock"] = 0x3A,
        ["ShiftLeft"] = 0x2A, ["ShiftRight"] = 0x36, ["ControlLeft"] = 0x1D, ["ControlRight"] = 0xE01D,
        ["AltLeft"] = 0x38, ["AltRight"] = 0xE038, ["MetaLeft"] = 0xE05B, ["MetaRight"] = 0xE05C, ["ContextMenu"] = 0xE05D,
        ["PrintScreen"] = 0xE037, ["ScrollLock"] = 0x46,

        // F tuşları
        ["F1"] = 0x3B, ["F2"] = 0x3C, ["F3"] = 0x3D, ["F4"] = 0x3E, ["F5"] = 0x3F, ["F6"] = 0x40, ["F7"] = 0x41, ["F8"] = 0x42,
        ["F9"] = 0x43, ["F10"] = 0x44, ["F11"] = 0x57, ["F12"] = 0x58,

        // Gezinme (genişletilmiş)
        ["Insert"] = 0xE052, ["Delete"] = 0xE053, ["Home"] = 0xE047, ["End"] = 0xE04F, ["PageUp"] = 0xE049, ["PageDown"] = 0xE051,
        ["ArrowUp"] = 0xE048, ["ArrowDown"] = 0xE050, ["ArrowLeft"] = 0xE04B, ["ArrowRight"] = 0xE04D,

        // Sayısal tuş takımı
        ["NumLock"] = 0xE045, ["NumpadDivide"] = 0xE035, ["NumpadMultiply"] = 0x37, ["NumpadSubtract"] = 0x4A, ["NumpadAdd"] = 0x4E,
        ["NumpadEnter"] = 0xE01C, ["NumpadDecimal"] = 0x53, ["NumpadEqual"] = 0x59,
        ["Numpad0"] = 0x52, ["Numpad1"] = 0x4F, ["Numpad2"] = 0x50, ["Numpad3"] = 0x51, ["Numpad4"] = 0x4B,
        ["Numpad5"] = 0x4C, ["Numpad6"] = 0x4D, ["Numpad7"] = 0x47, ["Numpad8"] = 0x48, ["Numpad9"] = 0x49,
    };

    /// <summary> Kod tabloda mı (sunucu yalnızca bunları iletir). </summary>
    public static bool IsKnown(string? code) => code is not null && Map.ContainsKey(code);

    /// <summary> <paramref name="scanCode"/>: alt bayt; <paramref name="extended"/>: <c>0xE0</c> önekli tuş. </summary>
    public static bool TryGet(string? code, out ushort scanCode, out bool extended)
    {
        if (code is not null && Map.TryGetValue(code, out ushort value))
        {
            scanCode = (ushort)(value & 0xFF);
            extended = (value & 0xFF00) == 0xE000;
            return true;
        }
        scanCode = 0;
        extended = false;
        return false;
    }
}
