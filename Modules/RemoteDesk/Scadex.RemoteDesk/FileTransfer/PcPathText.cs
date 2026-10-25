namespace Scadex.RemoteDesk.FileTransfer;

/// <summary>
/// PC'deki yolların metni — sunucunun işletim sisteminden BAĞIMSIZ. PC Windows da olabilir, ileride Linux/Android da; sunucu da IIS'te ya
/// da Linux'ta çalışabilir. <c>Path.GetFileName</c> / <c>Path.Combine</c> / <c>Path.GetInvalidFileNameChars</c> sunucunun OS kuralını
/// uygular (Linux'ta <c>\</c> ayırıcı değildir, Windows'ta <c>/home/x</c> + <c>a.txt</c> → <c>/home/x\a.txt</c>), bu yüzden PC yolu
/// onlarla işlenmez. Yol PC için opak bir metindir; doğrulama ve yazma politikası PC'nindir — burası yalnızca ad çıkarma ve günlük içindir.
/// </summary>
public static class PcPathText
{
    /// <summary>
    /// Hiçbir platformda dosya adında olmaması gereken karakterler: Windows'un yasak kümesi (en dar küme) + kontrol karakterleri.
    /// Sunucunun OS'una göre değişmez — ad hem tarayıcıya (<c>Content-Disposition</c>) hem herhangi bir PC'ye gider.
    /// </summary>
    private static readonly char[] InvalidFileNameChars =
        [.. "<>:\"/\\|?*", .. Enumerable.Range(0, 32).Select(i => (char)i)];

    private static readonly char[] Separators = ['\\', '/'];

    /// <summary> Yolun son parçası (hem <c>\</c> hem <c>/</c> ayırıcıdır; sondaki ayırıcılar atılır). </summary>
    public static string FileName(string pcPath)
    {
        string trimmed = pcPath.TrimEnd(Separators);
        int index = trimmed.LastIndexOfAny(Separators);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    /// <summary> Klasör + ad. Ayırıcı klasörün kendi ayırıcısıdır: yalnızca <c>\</c> içeriyorsa <c>\</c>, aksi hâlde <c>/</c>. </summary>
    public static string Join(string folder, string name)
    {
        char separator = folder.Contains('\\') && !folder.Contains('/') ? '\\' : '/';
        return folder.Length > 0 && folder[^1] is '\\' or '/' ? folder + name : folder + separator + name;
    }

    /// <summary> Adı tek bir dosya adına indirger ve yasak karakterleri atar; boş kalırsa <c>"dosya"</c>. </summary>
    public static string SafeFileName(string name)
    {
        string cleaned = string.Concat(FileName(name).Split(InvalidFileNameChars)).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "dosya" : cleaned;
    }
}
