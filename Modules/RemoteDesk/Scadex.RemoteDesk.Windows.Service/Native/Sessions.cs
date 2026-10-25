using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Scadex.RemoteDesk.Windows.Service.Native;

/// <summary> Windows oturumları (WTS). Servis hedef oturumu seçer, Agent kendi oturumunun kullanıcısını sorar. </summary>
internal static class Sessions
{
    public sealed record SessionEntry(int Id, Win32.WtsConnectState State, string UserName);

    public static IReadOnlyList<SessionEntry> Enumerate()
    {
        if (!Win32.WTSEnumerateSessions(Win32.WtsCurrentServer, 0, 1, out IntPtr buffer, out int count))
            throw new Win32Exception();

        try
        {
            int size = Marshal.SizeOf<Win32.WTS_SESSION_INFO>();
            var sessions = new List<SessionEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<Win32.WTS_SESSION_INFO>(buffer + i * size);
                sessions.Add(new SessionEntry(info.SessionId, info.State, QueryUserName(info.SessionId)));
            }
            return sessions;
        }
        finally
        {
            Win32.WTSFreeMemory(buffer);
        }
    }

    /// <summary> <c>DOMAIN\kullanıcı</c>; oturumda kimse yoksa (giriş ekranı, Session 0) boş. </summary>
    public static string QueryUserName(int sessionId)
    {
        string user = QueryString(sessionId, Win32.WTSUserName);
        if (user.Length == 0) return "";
        string domain = QueryString(sessionId, Win32.WTSDomainName);
        return domain.Length > 0 ? $@"{domain}\{user}" : user;
    }

    /// <summary>
    /// Agent'ın çalışacağı oturum (kullanıcı kararı 2026-10-25): <b>aktif ve kullanıcısı olan</b> oturum — önce fiziksel konsol, yoksa en küçük
    /// Id'li aktif uzak (RDP) oturum. Hiçbiri yoksa <c>null</c>: kimse oturum açmamışken (giriş ekranı) ya da tek oturum bağlantısı kesilmiş
    /// RDP iken Agent çalışmaz ve PC merkezde "bağlı değil" görünür. Kilitli oturum hâlâ aktiftir (Agent kalır, yayın "ekran kilitli").
    /// </summary>
    public static int? SelectTarget(IReadOnlyList<SessionEntry> sessions)
    {
        var candidates = sessions.Where(s => s.Id != 0 && s.State == Win32.WtsConnectState.Active && s.UserName.Length > 0).ToList();
        if (candidates.Count == 0) return null;

        int console = Win32.WTSGetActiveConsoleSessionId();
        return candidates.Any(s => s.Id == console) ? console : candidates.Min(s => s.Id);
    }

    private static string QueryString(int sessionId, int infoClass)
    {
        if (!Win32.WTSQuerySessionInformation(Win32.WtsCurrentServer, sessionId, infoClass, out IntPtr buffer, out _))
            return "";
        try { return Marshal.PtrToStringUni(buffer) ?? ""; }
        finally { Win32.WTSFreeMemory(buffer); }
    }
}
