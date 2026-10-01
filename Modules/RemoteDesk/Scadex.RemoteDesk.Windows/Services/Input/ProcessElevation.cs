using System.Runtime.InteropServices;

namespace Scadex.RemoteDesk.Windows.Services.Input;

/// <summary>
/// İstemci yükseltilmiş (yönetici / high integrity) yetkiyle mi çalışıyor?. Yükseltilmişse yönetici olarak
/// çalışan pencerelere de (ör. Görev Yöneticisi) <c>SendInput</c> ile girdi gönderilebilir — UIPI engeli olmaz. Bir kez hesaplanır.
/// </summary>
internal static partial class ProcessElevation
{
    public static bool SelfElevated { get; } = Query();

    private static unsafe bool Query()
    {
        nint process = GetCurrentProcess();
        if (!OpenProcessToken(process, TOKEN_QUERY, out nint token))
            return false;
        try
        {
            uint elevated = 0;
            return GetTokenInformation(token, TokenElevation, &elevated, sizeof(uint), out _) && elevated != 0;
        }
        finally { CloseHandle(token); }
    }

    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint access, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetTokenInformation(nint token, int infoClass, void* info, uint length, out uint returnLength);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
