using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Agent;

/// <summary>
/// Agent'ın çalıştığı Windows oturumu. Agent SYSTEM'dir: <c>Environment.UserName</c> "SYSTEM", <c>SpecialFolder.Startup</c> systemprofile'ın
/// olurdu. Oturumdaki kullanıcıya ait her şey (Hello'daki kullanıcı adı, dosya transferinin reddettiği Startup klasörü, pipe ACL'indeki
/// logon SID) buradan okunur
/// <para/>
/// Geliştirmede Agent normal kullanıcıyla da çalıştırılabilir (<c>DOTNET_ENVIRONMENT=Development</c>); o zaman <c>WTSQueryUserToken</c>
/// kullanılamaz ve değerler sürecin kendi token'ından okunur.
/// </summary>
public sealed unsafe class SessionContext
{
    public SessionContext()
    {
        SessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        IsSystem = WindowsIdentity.GetCurrent().IsSystem;
        string user = Sessions.QueryUserName(SessionId);
        UserName = user.Length > 0 ? user : Environment.UserName;
    }

    public int SessionId { get; }

    /// <summary> Oturumdaki kullanıcı (<c>DOMAIN\kullanıcı</c>). </summary>
    public string UserName { get; }

    /// <summary> Süreç LocalSystem mi (üretim). Değilse geliştirme modudur. </summary>
    public bool IsSystem { get; }

    /// <summary> Oturumdaki kullanıcının "Başlangıç" klasörü — PC'ye yazmada korunan köklerden biri (§ 12.8). </summary>
    public string? UserStartupFolder()
    {
        if (!IsSystem)
            return Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        if (!Win32.WTSQueryUserToken(SessionId, out IntPtr token))
            return null;
        try
        {
            if (Win32.SHGetKnownFolderPath(Win32.FolderIdStartup, 0, token, out IntPtr path) != 0)
                return null;
            try { return Marshal.PtrToStringUni(path); }
            finally { Marshal.FreeCoTaskMem(path); }
        }
        finally { Win32.CloseHandle(token); }
    }

    /// <summary>
    /// Oturumun logon SID'i (<c>S-1-5-5-X-Y</c>): pipe yalnızca bu oturumdaki kullanıcının süreçlerine açılır — aynı kullanıcı başka bir
    /// oturumda olsa bile o oturumun logon SID'i farklıdır.
    /// </summary>
    public SecurityIdentifier LogonSid()
    {
        IntPtr token;
        if (IsSystem)
        {
            if (!Win32.WTSQueryUserToken(SessionId, out token)) throw new Win32Exception();
        }
        else if (!Win32.OpenProcessToken(Win32.GetCurrentProcess(), Win32.TOKEN_QUERY, out token))
        {
            throw new Win32Exception();
        }

        try
        {
            Win32.GetTokenInformation(token, Win32.TokenLogonSid, null, 0, out int length);
            byte* buffer = stackalloc byte[length];
            if (!Win32.GetTokenInformation(token, Win32.TokenLogonSid, buffer, length, out _))
                throw new Win32Exception();

            // TOKEN_GROUPS { DWORD GroupCount; SID_AND_ATTRIBUTES Groups[] } — x64'te dizi 8 bayt hizalıdır.
            int count = *(int*)buffer;
            if (count < 1) throw new InvalidOperationException("Oturumun logon SID'i yok.");
            var group = *(Win32.SID_AND_ATTRIBUTES*)(buffer + IntPtr.Size);
            return new SecurityIdentifier(group.Sid);
        }
        finally
        {
            Win32.CloseHandle(token);
        }
    }
}
