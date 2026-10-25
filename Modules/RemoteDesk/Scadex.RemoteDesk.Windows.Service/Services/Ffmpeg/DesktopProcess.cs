using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Services.Ffmpeg;

/// <summary>
/// FFmpeg'i BELİRLİ BİR MASAÜSTÜNDE başlatır. <c>ddagrab</c> (DXGI) yalnızca kendi masaüstünü yakalar; UAC onayı ve
/// kilit ekranı <c>winsta0\Winlogon</c>'dadır. <see cref="Process.Start(ProcessStartInfo)"/> masaüstü seçemediği için süreç
/// <c>CreateProcessW</c> + <c>STARTUPINFO.lpDesktop</c> ile açılır; stdin/stdout/stderr anonim pipe'larla yönlendirilir.
/// <para/>
/// Agent'taki BÜTÜN FFmpeg başlatmaları buradan geçer ve tek kilitte sıralanır: kalıtılabilir pipe uçları süreç başlayana kadar açıktır,
/// aynı anda başlayan ikinci bir çocuk onları da kalıtsaydı pipe hiç kapanmaz (EOF gelmez). <c>Process.Start</c> da aynı nedenle kilitler —
/// Agent'ta FFmpeg için <c>Process.Start</c> kullanmayın. Süreç askıda başlar, Job Object'e konur, sonra sürer.
/// </summary>
public sealed unsafe class DesktopProcess : IDisposable
{
    private static readonly Lock StartLock = new();

    private readonly IntPtr _handle;

    private DesktopProcess(IntPtr handle, Process process, StreamWriter stdin, StreamReader stdout, StreamReader stderr, string desktop)
    {
        _handle = handle;
        Process = process;
        StandardInput = stdin;
        StandardOutput = stdout;
        StandardError = stderr;
        Desktop = desktop;
    }

    /// <summary> Süreç sorguları (<c>HasExited</c>, <c>ExitCode</c>, <c>Kill</c>, <c>PriorityClass</c>, CPU/bellek, <c>WaitForExitAsync</c>). </summary>
    public Process Process { get; }
    public StreamWriter StandardInput { get; }
    public StreamReader StandardOutput { get; }
    public StreamReader StandardError { get; }

    /// <summary> Sürecin bağlı olduğu masaüstü (<c>Default</c> / <c>Winlogon</c> …). </summary>
    public string Desktop { get; }

    /// <param name="job"><c>null</c> = job'a konmaz (yalnızca kısa ömürlü sürüm sorgusu).</param>
    public static DesktopProcess Start(string exe, IEnumerable<string> arguments, string desktop, ChildProcessJob? job)
    {
        var stdin = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var pi = new Win32.PROCESS_INFORMATION();
        try
        {
            char[] commandLine = BuildCommandLine(exe, arguments).ToCharArray();
            string desktopPath = $@"winsta0\{desktop}";

            lock (StartLock)
            {
                fixed (char* cmd = commandLine)
                fixed (char* desk = desktopPath)
                {
                    var si = new Win32.STARTUPINFOW
                    {
                        cb = sizeof(Win32.STARTUPINFOW),
                        lpDesktop = (IntPtr)desk,
                        dwFlags = Win32.STARTF_USESTDHANDLES,
                        hStdInput = stdin.ClientSafePipeHandle.DangerousGetHandle(),
                        hStdOutput = stdout.ClientSafePipeHandle.DangerousGetHandle(),
                        hStdError = stderr.ClientSafePipeHandle.DangerousGetHandle(),
                    };
                    if (!Win32.CreateProcess(null, cmd, IntPtr.Zero, IntPtr.Zero, true,
                            Win32.CREATE_SUSPENDED | Win32.CREATE_NO_WINDOW, IntPtr.Zero, null, &si, &pi))
                        throw new Win32Exception();
                }
                // Çocuk uçlarını kapat: çocuk çıkınca okuma EOF görsün; sonraki başlatmalar bunları kalıtmasın.
                stdin.DisposeLocalCopyOfClientHandle();
                stdout.DisposeLocalCopyOfClientHandle();
                stderr.DisposeLocalCopyOfClientHandle();
            }

            job?.Add(pi.hProcess);
            var process = Process.GetProcessById(pi.dwProcessId);   // tanıtıcımız açık: Id başka sürece geçemez
            Win32.ResumeThread(pi.hThread);
            Win32.CloseHandle(pi.hThread);
            pi.hThread = IntPtr.Zero;

            var result = new DesktopProcess(pi.hProcess, process,
                new StreamWriter(stdin, new UTF8Encoding(false)) { AutoFlush = false },
                new StreamReader(stdout, System.Text.Encoding.UTF8),
                new StreamReader(stderr, System.Text.Encoding.UTF8),
                desktop);
            pi.hProcess = IntPtr.Zero;
            return result;
        }
        catch
        {
            if (pi.hProcess != IntPtr.Zero) { Win32.TerminateProcess(pi.hProcess, 1); Win32.CloseHandle(pi.hProcess); }
            if (pi.hThread != IntPtr.Zero) Win32.CloseHandle(pi.hThread);
            stdin.Dispose();
            stdout.Dispose();
            stderr.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        StandardInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
        Process.Dispose();
        Win32.CloseHandle(_handle);
    }

    /// <summary> <c>ProcessStartInfo.ArgumentList</c> ile aynı kurallar (CommandLineToArgvW tersine çevrilebilir tırnaklama). </summary>
    private static string BuildCommandLine(string exe, IEnumerable<string> arguments)
    {
        var sb = new StringBuilder();
        AppendArgument(sb, exe);
        foreach (string arg in arguments)
        {
            sb.Append(' ');
            AppendArgument(sb, arg);
        }
        return sb.Append('\0').ToString();
    }

    private static void AppendArgument(StringBuilder sb, string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        for (int i = 0; i < arg.Length; i++)
        {
            int backslashes = 0;
            while (i < arg.Length && arg[i] == '\\') { backslashes++; i++; }

            if (i == arg.Length) { sb.Append('\\', backslashes * 2); break; }
            if (arg[i] == '"') sb.Append('\\', backslashes * 2 + 1).Append('"');
            else sb.Append('\\', backslashes).Append(arg[i]);
        }
        sb.Append('"');
    }
}
