using Microsoft.Win32.SafeHandles;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Services.FileTransfer;

/// <summary>
/// Dosya transferinin yol kuralları. Agent <b>SYSTEM</b> çalıştığı için yol denetimi tek savunmadır:
/// <list type="bullet">
/// <item><b>Okuma / gezme serbesttir</b> (kullanıcı kararı) — yalnızca ağ ve aygıt yolları (<c>\\sunucu\paylaşım</c>, <c>\\?\</c>, <c>\\.\</c>)
/// reddedilir: SYSTEM ağa makine hesabıyla çıkar (kullanıcının göremediği paylaşımlar açılırdı) ve aygıt yoluyla ham disk okunabilirdi.</item>
/// <item><b>Yazma</b>: yalın dosya adı (ayraç, <c>..</c>, ADS <c>:</c>, ayrılmış ad yok), tam nitelikli yerel yol, yol üzerinde junction /
/// sembolik bağ yok, korumalı köklerin (Windows, Program Files, Başlangıç klasörleri, kurulum klasörü, <c>%ProgramData%\Scadex</c>) dışı.
/// Dosya açıldıktan sonra gerçek yolu (<see cref="FinalPath"/>) yeniden denetlenir — yarış ve bilinmeyen yönlendirmeler için.</item>
/// </list>
/// Reddin metni sabittir ve merkeze/operatöre gider; ayrıntı yalnızca günlüğe yazılır.
/// </summary>
public sealed class FilePathPolicy
{
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    ];

    private readonly string[] _protectedRoots;

    public FilePathPolicy(Agent.SessionContext session)
    {
        string installRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        _protectedRoots =
        [
            .. new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                // SYSTEM'in kendi Başlangıç klasörü değil, OTURUMDAKİ kullanıcınınki (§ 9.8).
                session.UserStartupFolder() ?? "",
                // Agent'ın kendi dosyaları ve günlükleri: üzerine yazılsa SYSTEM ile kalıcılık kazanılırdı.
                installRoot,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Scadex"),
            }
            .Where(r => r.Length > 0)
            .Select(r => r.TrimEnd('\\', '/'))
        ];
    }

    /// <summary> Gezme / okuma için: yalnızca ağ ve aygıt yolları reddedilir. <c>null</c> = izinli. </summary>
    public static string? RejectRead(string path) =>
        IsNetworkOrDevicePath(path) ? "Ağ ve aygıt yolları desteklenmiyor." :
        HasStreamSeparator(path) ? "Geçersiz yol." :
        null;

    /// <summary> PC'ye yazmadan önce. <c>null</c> = izinli; <paramref name="fullPath"/> hedef dosyanın tam yoludur. </summary>
    public string? RejectWrite(string directory, string fileName, out string fullPath)
    {
        fullPath = "";
        if (!IsPlainFileName(fileName))
            return "Geçersiz dosya adı.";
        if (string.IsNullOrWhiteSpace(directory) || IsNetworkOrDevicePath(directory) || HasStreamSeparator(directory) || !Path.IsPathFullyQualified(directory))
            return "Geçersiz hedef klasör.";

        try { fullPath = Path.GetFullPath(Path.Combine(directory, fileName)); }
        catch { return "Geçersiz hedef yol."; }

        if (IsProtected(fullPath))
            return "Bu klasöre yazma kapalı (sistem klasörü).";
        if (!Directory.Exists(directory))
            return "Hedef klasör yok.";
        if (HasLinkOnPath(Path.GetDirectoryName(fullPath)!) || IsLink(new FileInfo(fullPath)))
            return "Hedef yolda bağlantı (junction / sembolik bağ) var; yazma kapalı.";
        return null;
    }

    public bool IsProtected(string fullPath)
    {
        foreach (var root in _protectedRoots)
        {
            if (fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary> Açık dosyanın işletim sistemine göre gerçek yolu (bağlar çözülmüş, <c>\\?\</c> öneki atılmış). </summary>
    public static unsafe string? FinalPath(SafeFileHandle file)
    {
        const int capacity = 32_768;
        char[] buffer = new char[capacity];
        uint length;
        fixed (char* p = buffer)
            length = Win32.GetFinalPathNameByHandle(file, p, capacity, Win32.FILE_NAME_NORMALIZED);
        if (length == 0 || length >= capacity)
            return null;

        string path = new(buffer, 0, (int)length);
        if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + path[8..];
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path[4..];
        return path;
    }

    private static bool IsNetworkOrDevicePath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    /// <summary> <c>C:</c>'deki iki nokta dışında <c>:</c> = alternatif veri akışı (ADS). </summary>
    private static bool HasStreamSeparator(string path) => path.IndexOf(':', 2) >= 0;

    private static bool IsPlainFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' '))
            return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(':'))
            return false;
        string stem = Path.GetFileNameWithoutExtension(name).TrimEnd(' ');
        return !ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Klasörden köke kadar herhangi bir bileşen junction ya da sembolik bağ mı. <c>LinkTarget</c> yalnızca bu ikisinde doludur;
    /// OneDrive gibi bulut yer tutucularının reparse noktaları bağ sayılmaz (Masaüstü OneDrive'a yönlendirilmiş olabilir).
    /// </summary>
    private static bool HasLinkOnPath(string directory)
    {
        for (var dir = new DirectoryInfo(directory); dir is not null; dir = dir.Parent)
        {
            if (dir.Exists && IsLink(dir)) return true;
        }
        return false;
    }

    private static bool IsLink(FileSystemInfo info)
    {
        try { return info.Exists && info.LinkTarget is not null; }
        catch { return true; }   // okunamayan bağ bilgisi: güvenli tarafta kal
    }
}
