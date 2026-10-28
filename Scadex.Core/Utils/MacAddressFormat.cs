namespace Scadex.Core.Utils;

/// <summary>
/// <c>ControlCard.MacAddress</c> ve SCADA'nın <c>DeviceId</c> header'ı TEK TİP karşılaştırılır: <c>AA:BB:CC:DD:EE:FF</c> (büyük harf, <c>:</c> ayraç).
/// Sahada adres <c>50-8D-5C-AC-39-1F</c>, <c>50:8d:5c:ac:39:1f</c> ya da <c>508D5CAC391F</c> diye yazılabiliyor; ayraç farkı
/// SCADA eşleşmesini (kart MAC'ten çözülür) bozardı.
/// </summary>
public static class MacAddressFormat
{
    public const char Separator = ':';

    /// <summary> Ayraçlar (<c>- : .</c> ve boşluk) atılıp 12 harf/rakam kalıyorsa <c>AA:BB:CC:DD:EE:FF</c>; kalmıyorsa <c>null</c>. </summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Span<char> compact = stackalloc char[12];
        int count = 0;
        foreach (char c in value)
        {
            if (c is '-' or ':' or '.' or ' ')
                continue;
            if (!char.IsAsciiLetterOrDigit(c) || count == compact.Length)
                return null;
            compact[count++] = char.ToUpperInvariant(c);
        }
        if (count != compact.Length)
            return null;

        return string.Create(17, compact.ToArray(), (dest, hex) =>
        {
            for (int i = 0, j = 0; i < 12; i += 2)
            {
                if (i > 0) dest[j++] = Separator;
                dest[j++] = hex[i];
                dest[j++] = hex[i + 1];
            }
        });
    }

    /// <summary>
    /// Giriş DTO'larının setter'ı için: boş → <c>null</c>, biçimlenebiliyorsa tek tip, değilse kırpılmış ham değer — ham değeri
    /// doğrulayıcı reddeder.
    /// </summary>
    public static string? NormalizeOrKeep(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TryNormalize(value) ?? value.Trim();
}
