using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Windows.Service.Native;

namespace Scadex.RemoteDesk.Windows.Service.Services.Desktop;

/// <summary>
/// Oturumda kullanıcı girdisini o an alan masaüstü: normalde <c>Default</c>, UAC onay penceresi ve kilit ekranında
/// <c>Winlogon</c> ("güvenli masaüstü"), ekran koruyucuda <c>Screen-saver</c>. DXGI yakalaması ve <c>SendInput</c> çağıran iş parçacığının
/// masaüstüne gider; yayın (<c>DesktopProcess</c>) ve girdi (<c>RemoteInputService</c>) bu adı izleyip doğru masaüstüne geçer.
/// <para/>
/// <b>Boşta iş yok:</b> yalnızca en az bir yayın ya da kontrol oturumu <see cref="Acquire"/> etmişken 300 ms'de bir yoklar ve
/// <see cref="Changed"/>'ı tetikler; kimse yokken <see cref="Current"/> her okunuşta taze sorulur. Güvenli masaüstünü yalnızca SYSTEM açabilir —
/// geliştirme modunda (normal kullanıcı) ad hep <see cref="Default"/> görünür ve davranış eskisi gibidir ("ekran kilitli").
/// </summary>
public sealed unsafe class InputDesktopWatcher : IDisposable
{
    public const string Default = "Default";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);

    private readonly ILogger<InputDesktopWatcher> _logger;
    private readonly Lock _gate = new();
    private Timer? _timer;
    private int _users;
    private string _current = Default;

    public InputDesktopWatcher(ILogger<InputDesktopWatcher> logger) => _logger = logger;

    /// <summary> Aktif masaüstünün adı değişti (zamanlayıcı iş parçacığından). </summary>
    public event Action<string>? Changed;

    public string Current
    {
        get
        {
            lock (_gate)
            {
                if (_users == 0) _current = Read();
                return _current;
            }
        }
    }

    /// <summary> Yoklamayı açık tutar; dönen nesne bırakılınca (son kullanıcıda) yoklama durur. </summary>
    public IDisposable Acquire()
    {
        lock (_gate)
        {
            if (_users++ == 0)
            {
                _current = Read();
                _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
            }
        }
        return new Lease(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            if (--_users > 0) return;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Poll()
    {
        string name = Read();
        lock (_gate)
        {
            if (name == _current || _users == 0) return;
            _logger.LogInformation("Aktif masaüstü değişti: {From} → {To}", _current, name);
            _current = name;
        }
        try { Changed?.Invoke(name); }
        catch (Exception ex) { _logger.LogWarning(ex, "Masaüstü değişimi işlenemedi"); }
    }

    /// <summary> Giriş masaüstünün adı; açılamazsa (SYSTEM değil, geçiş anı) <see cref="Default"/>. </summary>
    public static string Read()
    {
        IntPtr desktop = Win32.OpenInputDesktop(0, false, Win32.DESKTOP_READOBJECTS);
        if (desktop == IntPtr.Zero) return Default;
        try
        {
            char* name = stackalloc char[64];
            return Win32.GetUserObjectInformation(desktop, Win32.UOI_NAME, name, 64 * sizeof(char), out _)
                ? new string(name)
                : Default;
        }
        finally { Win32.CloseDesktop(desktop); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private sealed class Lease(InputDesktopWatcher owner) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) owner.Release();
        }
    }
}
