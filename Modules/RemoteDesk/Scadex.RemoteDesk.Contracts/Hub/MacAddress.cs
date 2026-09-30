namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary> PC eşleşmesinde MAC karşılaştırması NORMALİZE edilir <c>aa:bb-CC…</c> → <c>AABBCC…</c> </summary>
public static class MacAddress
{
    /// <summary> 12 onaltılık hane değilse <c>null</c> (geçersiz ya da boş adres eşleşmeye katılmaz). </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Span<char> hex = stackalloc char[12];
        int count = 0;
        foreach (char c in value)
        {
            if (!Uri.IsHexDigit(c))
                continue;
            if (count == hex.Length)
                return null;
            hex[count++] = char.ToUpperInvariant(c);
        }

        return count == hex.Length ? new string(hex) : null;
    }

    /// <summary> Ekranda gösterim: <c>AA:BB:CC:DD:EE:FF</c>. </summary>
    public static string Format(string normalized) =>
        string.Join(':', Enumerable.Range(0, normalized.Length / 2).Select(i => normalized.Substring(i * 2, 2)));
}
