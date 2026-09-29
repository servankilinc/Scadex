using System.Text.RegularExpressions;

namespace Scadex.RemoteDesk.Windows.Services.Encoding;

/// <summary>
/// FFmpeg hata çıktısını sahada anlaşılır bir nedene çevirir.
/// Ham stderr arayüze ve sunucuya gitmez: içinde yayın adresi/token olabilir.
/// </summary>
public static partial class FailureExplainer
{
    /// <summary> Ekran kilitli ya da masaüstü değişti (UAC/kilit) — kodlayıcı hatası değildir, kilit açılınca geçer. </summary>
    public const string ScreenLocked = "Ekran yakalanamıyor: ekran kilitli ya da masaüstü değişti";

    public static bool IsScreenLocked(string stderr) => ScreenLockedRegex().IsMatch(stderr);

    public static string Explain(string stderr)
    {
        // Sıra önemli, ilk eşleşen kazanır. Kilit kontrolü "Operation not permitted"i yalnızca GİRDİ açılırken
        // arar: NVENC de kendi hatasında aynı ifadeyi kullanır.
        if (IsScreenLocked(stderr)) return ScreenLocked;

        if (NvencApiRegex().IsMatch(stderr))
        {
            Match api = NvencVersionsRegex().Match(stderr);
            Match min = NvencMinDriverRegex().Match(stderr);
            return $"NVIDIA sürücüsü eski: bu FFmpeg NVENC API {(api.Success ? api.Groups[1].Value : "?")} istiyor, " +
                   $"sürücü {(api.Success ? api.Groups[2].Value : "?")} sunuyor" +
                   (min.Success ? $" — sürücüyü {min.Groups[1].Value} ya da üstüne güncelleyin" : "");
        }
        if (Contains(stderr, "nvcuda.dll", "No capable devices found", "nvEncodeAPI")) return "NVIDIA ekran kartı / sürücüsü yok";
        if (Contains(stderr, "amfrt64.dll")) return "AMD ekran kartı / sürücüsü yok";
        if (Contains(stderr, "runtime doesn't support", "is unsupported", "not supported by the QSV runtime"))
            return "Intel sürücüsü bu kodlayıcıyı sunmuyor (daha yeni Intel nesli gerekir)";
        if (Contains(stderr, "Could not create the texture", "frame pool", "child frames context"))
            return "Sürücü GPU belleğinde kare havuzu açamadı (bu zincir desteklenmiyor)";
        if (Contains(stderr, "Impossible to convert between the formats"))
            return "Kare biçimi dönüştürülemiyor (zincir bu kodlayıcıya uymuyor)";
        if (Contains(stderr, "Unknown encoder")) return "Bu FFmpeg build'inde kodlayıcı yok";
        if (Contains(stderr, "Connection refused", "Server returned", "Connection timed out"))
            return "Yayın adresine bağlanılamadı (MediaMTX çalışıyor mu, adres doğru mu?)";

        string? last = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => !l.Contains("Conversion failed") && !l.Contains("Terminating thread") && !l.Contains("Task finished"));
        return last is null ? "Bilinmeyen hata" : Mask(PrefixRegex().Replace(last, ""));
    }

    /// <summary> Yayın adresindeki kimlik bilgisini/bileti gizler (CameraCaptureGateway ile aynı kural). </summary>
    public static string Mask(string text) => CredentialRegex().Replace(text, "$1***@");

    private static bool Contains(string s, params string[] needles) =>
        needles.Any(n => s.Contains(n, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"Error opening input[\s\S]*Operation not permitted|887a0026", RegexOptions.IgnoreCase)]
    private static partial Regex ScreenLockedRegex();
    [GeneratedRegex(@"required nvenc API version|minimum required Nvidia driver", RegexOptions.IgnoreCase)]
    private static partial Regex NvencApiRegex();
    [GeneratedRegex(@"Required: ([\d.]+) Found: ([\d.]+)")]
    private static partial Regex NvencVersionsRegex();
    [GeneratedRegex(@"minimum required Nvidia driver for nvenc is ([\d.]+)")]
    private static partial Regex NvencMinDriverRegex();
    [GeneratedRegex(@"^\[[^\]]+\]\s*")]
    private static partial Regex PrefixRegex();
    [GeneratedRegex(@"(rtsps?://)[^@/\s]+@")]
    private static partial Regex CredentialRegex();
}
