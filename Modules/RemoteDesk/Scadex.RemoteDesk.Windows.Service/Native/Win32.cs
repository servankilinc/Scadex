using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Scadex.RemoteDesk.Windows.Service.Native;

/// <summary>
/// Servisin (oturum seçimi, Agent'ı kullanıcı oturumunda başlatma) ve Agent'ın (oturum kullanıcısı, pipe ACL'i, dosya yolu denetimi)
/// kullandığı Win32 çağrıları. Yalnızca gereken imzalar; sarmalayıcılar <see cref="Sessions"/> / <see cref="AgentLauncher"/> içindedir.
/// </summary>
internal static unsafe partial class Win32
{
    public static readonly IntPtr WtsCurrentServer = IntPtr.Zero;

    // ---- WTS (wtsapi32) ----

    public enum WtsConnectState { Active = 0, Connected = 1, ConnectQuery = 2, Shadow = 3, Disconnected = 4, Idle = 5, Listen = 6, Reset = 7, Down = 8, Init = 9 }

    public const int WTSUserName = 5;
    public const int WTSDomainName = 7;

    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public WtsConnectState State;
    }

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSEnumerateSessionsW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSEnumerateSessions(IntPtr hServer, int reserved, int version, out IntPtr sessionInfo, out int count);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, int infoClass, out IntPtr buffer, out int bytes);

    [LibraryImport("wtsapi32.dll")]
    public static partial void WTSFreeMemory(IntPtr memory);

    /// <summary> SYSTEM + SeTcbPrivilege ister; oturumdaki kullanıcının birincil token'ı. </summary>
    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSQueryUserToken(int sessionId, out IntPtr token);

    [LibraryImport("kernel32.dll")]
    public static partial int WTSGetActiveConsoleSessionId();

    // ---- Token / süreç (advapi32, kernel32) ----

    public const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    public const uint TOKEN_DUPLICATE = 0x0002;
    public const uint TOKEN_QUERY = 0x0008;
    public const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    public const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    public const uint MAXIMUM_ALLOWED = 0x02000000;
    public const int SecurityImpersonation = 2;
    public const int TokenPrimary = 1;
    public const int TokenSessionId = 12;
    public const int TokenLogonSid = 28;

    public const uint CREATE_SUSPENDED = 0x00000004;
    public const uint CREATE_NO_WINDOW = 0x08000000;
    public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public const uint STILL_ACTIVE = 259;

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOW
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GetCurrentProcess();

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DuplicateTokenEx(IntPtr existing, uint access, IntPtr attributes, int impersonationLevel, int tokenType, out IntPtr newToken);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetTokenInformation(IntPtr token, int infoClass, void* info, int length);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(IntPtr token, int infoClass, void* info, int length, out int returnLength);

    [LibraryImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcessAsUser(IntPtr token, char* applicationName, char* commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment,
        char* currentDirectory, STARTUPINFOW* startupInfo, PROCESS_INFORMATION* processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint ResumeThread(IntPtr thread);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(IntPtr process, uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcess(char* applicationName, char* commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, char* currentDirectory,
        STARTUPINFOW* startupInfo, PROCESS_INFORMATION* processInformation);

    public const int STARTF_USESTDHANDLES = 0x00000100;

    // ---- Masaüstü (user32) — UAC / kilit ekranı (güvenli masaüstü, Faz 11b) ----

    public const uint DESKTOP_READOBJECTS = 0x0001;
    public const uint GENERIC_ALL = 0x10000000;
    public const int UOI_NAME = 2;

    /// <summary> Kullanıcı girdisini o an alan masaüstü (<c>Default</c>, UAC/kilitte <c>Winlogon</c>). SYSTEM değilse Winlogon'da başarısız olur. </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint desiredAccess);

    [LibraryImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetUserObjectInformation(IntPtr obj, int index, void* info, uint length, out uint lengthNeeded);

    /// <summary> Çağıran iş parçacığını masaüstüne bağlar; iş parçacığının penceresi/kancası varsa başarısız olur (havuz iş parçacığında kullanılmaz). </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetThreadDesktop(IntPtr desktop);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseDesktop(IntPtr desktop);

    // ---- Pipe / dosya ----

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint sessionId);

    public const uint FILE_NAME_NORMALIZED = 0x0;

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    public static partial uint GetFinalPathNameByHandle(SafeFileHandle file, char* path, uint length, uint flags);

    // ---- Shell ----

    /// <summary> FOLDERID_Startup — kullanıcının "Başlangıç" klasörü (token'la, oturumdaki kullanıcınınki). </summary>
    public static readonly Guid FolderIdStartup = new("B97D20BB-F46A-4C97-BA10-5E3608430854");

    [LibraryImport("shell32.dll")]
    public static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
