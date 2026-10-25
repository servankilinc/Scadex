using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using Scadex.RemoteDesk.Windows.Service.Native;
using Scadex.RemoteDesk.Windows.Service.Services.Ffmpeg;

namespace Scadex.RemoteDesk.Windows.Service.Supervisor;

/// <summary> Çalışan bir Agent süreci. Servis ondan sorumludur; <see cref="Dispose"/> yalnızca tanıtıcıları bırakır. </summary>
public sealed class AgentProcess : IDisposable
{
    private readonly IntPtr _process;
    private readonly EventWaitHandle _stopSignal;
    private readonly TaskCompletionSource<uint> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly RegisteredWaitHandle _wait;
    private readonly ManualResetEvent _processWaitHandle;

    internal AgentProcess(IntPtr process, int processId, int sessionId, EventWaitHandle stopSignal)
    {
        _process = process;
        _stopSignal = stopSignal;
        ProcessId = processId;
        SessionId = sessionId;
        StartedUtc = DateTime.UtcNow;

        // Süreç tanıtıcısı beklenebilir bir nesnedir: çıkışı iş parçacığı bağlamadan, olayla öğrenilir.
        _processWaitHandle = new ManualResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(process, ownsHandle: false) };
        _wait = ThreadPool.RegisterWaitForSingleObject(_processWaitHandle, (_, _) =>
        {
            Win32.GetExitCodeProcess(_process, out uint code);
            _exited.TrySetResult(code);
        }, null, Timeout.Infinite, executeOnlyOnce: true);
    }

    public int ProcessId { get; }
    public int SessionId { get; }
    public DateTime StartedUtc { get; }

    /// <summary> Süreç bitince çıkış koduyla tamamlanır. </summary>
    public Task<uint> Exited => _exited.Task;

    /// <summary> Nazik durdurma: Agent olayı görünce yayınları (FFmpeg'e 'q') ve bağlantıyı kapatır; süre dolarsa zorla sonlandırılır. </summary>
    public async Task StopAsync(TimeSpan grace)
    {
        if (Exited.IsCompleted) return;
        _stopSignal.Set();
        if (await Task.WhenAny(Exited, Task.Delay(grace)) != Exited)
        {
            Win32.TerminateProcess(_process, 1);
            await Exited.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _wait.Unregister(null);
        _processWaitHandle.Dispose();
        _stopSignal.Dispose();
        Win32.CloseHandle(_process);
    }
}

/// <summary>
/// Agent'ı hedef kullanıcı oturumunda <b>SYSTEM</b> kimliğiyle başlatır: servisin kendi token'ı çoğaltılır, oturum Id'si
/// hedef oturuma çevrilir (SeTcbPrivilege — yalnızca LocalSystem'de vardır) ve süreç <c>winsta0\default</c> masaüstünde açılır. SYSTEM
/// olduğu için yönetici pencerelerine de girdi ulaşır (UIPI engeli yok) ve oturumdaki kullanıcı onu sonlandıramaz.
/// <para/>
/// Agent servisin Job Object'indedir (<c>KILL_ON_JOB_CLOSE</c>): servis süreci ne şekilde biterse bitsin Agent ve FFmpeg'leri kalmaz.
/// Komut satırına kullanıcıdan gelen hiçbir şey girmez.
/// </summary>
public sealed unsafe class AgentLauncher : IDisposable
{
    public const string SessionArgument = "--session";

    private readonly ChildProcessJob _job = new();
    private readonly ILogger<AgentLauncher> _logger;

    public AgentLauncher(ILogger<AgentLauncher> logger) => _logger = logger;

    /// <summary> Agent'ın durdurma olayı. Adı sürecin Id'sini taşır: geçişte iki Agent kısa süre yan yana olabilir. </summary>
    public static string StopSignalName(int processId) => $@"Global\Scadex.RemoteDesk.Agent.{processId}.Stop";

    public AgentProcess Start(int sessionId)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Süreç yolu okunamadı.");
        IntPtr token = IntPtr.Zero, primary = IntPtr.Zero;
        var pi = new Win32.PROCESS_INFORMATION();
        try
        {
            if (!Win32.OpenProcessToken(Win32.GetCurrentProcess(),
                    Win32.TOKEN_DUPLICATE | Win32.TOKEN_QUERY | Win32.TOKEN_ASSIGN_PRIMARY | Win32.TOKEN_ADJUST_DEFAULT | Win32.TOKEN_ADJUST_SESSIONID,
                    out token))
                throw new Win32Exception();
            if (!Win32.DuplicateTokenEx(token, Win32.MAXIMUM_ALLOWED, IntPtr.Zero, Win32.SecurityImpersonation, Win32.TokenPrimary, out primary))
                throw new Win32Exception();
            int session = sessionId;
            if (!Win32.SetTokenInformation(primary, Win32.TokenSessionId, &session, sizeof(int)))
                throw new Win32Exception(); // SeTcbPrivilege yok: servis LocalSystem olarak çalışmıyor

            // CreateProcessW komut satırını yerinde değiştirebilir: yazılabilir bir kopya verilir.
            char[] commandLine = $"\"{exe}\" {SessionArgument}\0".ToCharArray();
            string desktop = @"winsta0\default";
            string directory = AppContext.BaseDirectory;

            fixed (char* cmd = commandLine)
            fixed (char* desk = desktop)
            fixed (char* dir = directory)
            {
                var si = new Win32.STARTUPINFOW { cb = sizeof(Win32.STARTUPINFOW), lpDesktop = (IntPtr)desk };
                if (!Win32.CreateProcessAsUser(primary, null, cmd, IntPtr.Zero, IntPtr.Zero, false,
                        Win32.CREATE_SUSPENDED | Win32.CREATE_NO_WINDOW, IntPtr.Zero, dir, &si, &pi))
                    throw new Win32Exception();
            }

            // Askıdayken: önce job'a, sonra durdurma olayı — Agent ilk satırını çalıştırdığında ikisi de hazır olsun.
            _job.Add(pi.hProcess);
            // Varsayılan DACL (servis token'ı): SYSTEM ve Administrators açabilir; oturumdaki standart kullanıcı Agent'ı durduramaz.
            var stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset, StopSignalName(pi.dwProcessId));
            var agent = new AgentProcess(pi.hProcess, pi.dwProcessId, sessionId, stopSignal);

            Win32.ResumeThread(pi.hThread);
            _logger.LogInformation("Agent başlatıldı: süreç {ProcessId}, oturum {SessionId}", pi.dwProcessId, sessionId);
            pi.hProcess = IntPtr.Zero;   // sahipliği AgentProcess'e geçti
            return agent;
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) Win32.CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero)
            {
                // Hata yolunda yarım kalan askıdaki süreç bırakılmaz.
                Win32.TerminateProcess(pi.hProcess, 1);
                Win32.CloseHandle(pi.hProcess);
            }
            if (primary != IntPtr.Zero) Win32.CloseHandle(primary);
            if (token != IntPtr.Zero) Win32.CloseHandle(token);
        }
    }

    public void Dispose() => _job.Dispose();
}
