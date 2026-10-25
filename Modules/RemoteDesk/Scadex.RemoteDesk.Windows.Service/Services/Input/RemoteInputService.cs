using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Service.Native;
using Scadex.RemoteDesk.Windows.Service.Services.Desktop;
using Scadex.RemoteDesk.Windows.Service.Services.Monitors;

namespace Scadex.RemoteDesk.Windows.Service.Services.Input;

public interface IRemoteInputService
{
    /// <summary> Şu an kontrol eden kullanıcının adı; <c>null</c> = uzaktan kontrol yok. </summary>
    string? Controller { get; }

    /// <summary> Kontrol başladı/bitti — iş parçacığı havuzundan gelir, UI Dispatcher'a taşımalıdır. </summary>
    event Action<string?>? ControllerChanged;

    void Begin(ControlStartedCommand command);
    void End(Guid controlSessionId);

    /// <summary> Merkez bağlantısı koptu: kontrol biter, basılı düğmeler bırakılır. </summary>
    void EndAll();

    /// <summary> SignalR iş parçacığından çağrılır; yalnızca kuyruğa bırakır. </summary>
    void Enqueue(InputBatch batch);

    /// <summary>
    /// Kontrol oturumunun girdi durumu değişti (kilit ekranı / UAC, red, monitör yok — readiness P1.5). Yalnızca değişince, girdi işçisinin
    /// iş parçacığından tetiklenir; merkez bağlantısı bunu <c>ReportControlInput</c> ile iletir.
    /// </summary>
    event Action<ControlInputReport>? InputStateChanged;
}

/// <summary>
/// Merkezden gelen fare ve klavye olaylarını <c>SendInput</c> ile uygular.
/// <list type="bullet">
/// <item>Tek işçi, tek kuyruk: olay sırası korunur. Ardışık <c>Move</c>'lardan yalnızca sonuncusu uygulanır (son-durum); düğme, tekerlek ve
/// tuş olayları asla düşmez.</item>
/// <item>Tuşlar <c>KEYEVENTF_SCANCODE</c> ile basılır (<see cref="WindowsScanCodes"/>): karakteri PC'nin kendi klavye düzeni belirler
/// (Türkçe Q'da ğ/ş/ı doğru çıkar).</item>
/// <item>Koordinat: [0,1] → monitörün fiziksel dikdörtgeni (DXGI) → sanal masaüstü 0–65535 (<c>ABSOLUTE | VIRTUALDESK</c>). Sanal masaüstü
/// monitörlerin DXGI dikdörtgenlerinin birleşimidir; <c>GetSystemMetrics</c> kullanılmaz çünkü işlemin DPI farkındalığına göre ölçeklenir.</item>
/// <item>Takılı düğme/tuş emniyeti: kontrol bitince, bağlantı kopunca ya da basılı bir şey varken 5 sn olay gelmezse basılı düğme ve
/// tuşların hepsi bırakılır (basılı tutulan tuşu tarayıcı tekrarlayarak gönderdiği için sürekli basmak bu süreyi aşmaz).</item>
/// <item>Girdi yalnızca <c>ControlStarted</c> ile <c>ControlEnded</c> arasında, o oturumun kimliğiyle kabul edilir.</item>
/// <item><b>Masaüstü (Faz 11b, 2026-10-25):</b> <c>SendInput</c> çağıran iş parçacığının masaüstüne gider. İşçi bu yüzden KENDİ iş
/// parçacığındadır (havuz iş parçacığı masaüstü değiştiremez) ve her paketten önce aktif masaüstüne (<see cref="InputDesktopWatcher"/>) bağlanır:
/// UAC onay penceresi ve kilit ekranı <c>Winlogon</c>'dadır. Geçişte basılı kalanlar bırakılır.</item>
/// </list>
/// Agent SYSTEM olduğu için yönetici pencerelerine ve güvenli masaüstüne girdi ulaşır; geliştirme modunda (normal kullanıcı)
/// ikisine de ulaşmaz — Windows bunu sessizce engeller.
/// <para/>
/// <b>Girdi durumu (readiness P1.5):</b> her turun sonunda <see cref="ControlInputState"/> hesaplanır ve yalnızca değişince
/// <see cref="InputStateChanged"/> tetiklenir — operatör kilit ekranı / UAC'yi, reddi ya da kaybolan monitörü araç çubuğunda görür. Ek sistem
/// çağrısı yalnızca bir gönderim reddedildiğinde yapılır (giriş masaüstü açılabiliyor mu). Masaüstü değişimi girdi beklemeden yansır.
/// </summary>
public sealed partial class RemoteInputService(IMonitorService monitors, InputDesktopWatcher desktops, ILogger<RemoteInputService> logger)
    : BackgroundService, IRemoteInputService
{
    private static readonly TimeSpan StuckButtonTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(1);

    private readonly Channel<Work> _queue = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Lock _gate = new();
    private ControlStartedCommand? _active;
    /// <summary> Kontrol sürerken masaüstü yoklaması açık kalır (boştayken yoklanmaz). </summary>
    private IDisposable? _desktopLease;

    // Yalnızca işçi iş parçacığında kullanılır.
    private readonly HashSet<MouseButton> _pressed = [];
    private readonly HashSet<string> _pressedKeys = new(StringComparer.Ordinal);
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private DateTime _lastEventUtc;
    private IntPtr _desktopHandle;
    private string _desktop = "";

    // Girdi durumu (yalnızca işçi iş parçacığında): turdaki sonuçlar + oturum başına son bildirilen durum.
    private bool _turnSent, _turnSendFailed, _turnMonitorMissing;
    private Guid? _stateSessionId;
    private ControlInputState _state = ControlInputState.Ok;

    public string? Controller { get { lock (_gate) return _active?.UserName; } }
    public event Action<string?>? ControllerChanged;
    public event Action<ControlInputReport>? InputStateChanged;

    public void Begin(ControlStartedCommand command)
    {
        lock (_gate)
        {
            _active = command;
            _desktopLease ??= desktops.Acquire();
        }
        _queue.Writer.TryWrite(new Work(null, RefreshMonitors: true));
        logger.LogInformation("Uzaktan kontrol başladı: {User} (oturum {ControlSessionId})", command.UserName, command.ControlSessionId);
        ControllerChanged?.Invoke(command.UserName);
    }

    public void End(Guid controlSessionId)
    {
        lock (_gate)
        {
            if (_active?.ControlSessionId != controlSessionId) return;
            _active = null;
            ReleaseDesktopLease();
        }
        _queue.Writer.TryWrite(new Work(null, ReleaseAll: true));
        logger.LogInformation("Uzaktan kontrol bitti (oturum {ControlSessionId})", controlSessionId);
        ControllerChanged?.Invoke(null);
    }

    public void EndAll()
    {
        Guid? ended;
        lock (_gate)
        {
            ended = _active?.ControlSessionId;
            _active = null;
            ReleaseDesktopLease();
        }
        _queue.Writer.TryWrite(new Work(null, ReleaseAll: true));
        if (ended is not null)
        {
            logger.LogInformation("Uzaktan kontrol bitti: merkez bağlantısı koptu (oturum {ControlSessionId})", ended);
            ControllerChanged?.Invoke(null);
        }
    }

    public void Enqueue(InputBatch batch) => _queue.Writer.TryWrite(new Work(batch));

    /// <summary> Kilit ekranı / UAC açılıp kapanınca durum girdi beklemeden değerlendirilsin (yalnızca kontrol sürerken). </summary>
    private void OnDesktopChanged(string desktop)
    {
        bool controlling;
        lock (_gate) controlling = _active is not null;
        if (controlling) _queue.Writer.TryWrite(new Work(null, Evaluate: true));
    }

    private void ReleaseDesktopLease()
    {
        _desktopLease?.Dispose();
        _desktopLease = null;
    }

    /// <summary> İşçi KENDİ iş parçacığında koşar: <c>SetThreadDesktop</c> havuz iş parçacığında yapılamaz (pencere/kanca sahibi olabilir). </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        desktops.Changed += OnDesktopChanged;
        var thread = new Thread(() =>
        {
            try { RunWorker(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Uzaktan girdi işçisi durdu"); }
            finally
            {
                desktops.Changed -= OnDesktopChanged;
                done.TrySetResult();
            }
        })
        { IsBackground = true, Name = "Uzaktan girdi" };
        thread.Start();
        return done.Task;
    }

    private void RunWorker(CancellationToken stoppingToken)
    {
        var pending = new List<Work>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool hasWork;
                try
                {
                    var wait = _queue.Reader.WaitToReadAsync(stoppingToken).AsTask();
                    hasWork = wait.Wait(IdleCheck) && wait.Result;
                }
                catch (AggregateException) when (stoppingToken.IsCancellationRequested) { break; }

                if (!hasWork)
                {
                    if ((_pressed.Count > 0 || _pressedKeys.Count > 0) && DateTime.UtcNow - _lastEventUtc > StuckButtonTimeout)
                        ReleasePressed("5 sn olay gelmedi");
                    continue;
                }

                // Birikmiş her şey birlikte işlenir: arka arkaya gelen hareketler tek harekete iner.
                pending.Clear();
                while (_queue.Reader.TryRead(out var work))
                    pending.Add(work);
                AttachToInputDesktop();
                Process(pending);
            }
        }
        finally
        {
            ReleasePressed("uygulama kapanıyor");
            if (_desktopHandle != IntPtr.Zero) Win32.CloseDesktop(_desktopHandle);
        }
    }

    /// <summary>
    /// İşçi iş parçacığını kullanıcı girdisini o an alan masaüstüne bağlar (<c>Default</c> ↔ <c>Winlogon</c>: UAC onayı, kilit ekranı).
    /// Geçişte basılı kalan tuş/düğmeler eski masaüstünde bırakılır. Bağlanamazsa (SYSTEM değil — geliştirme modu) eskisinde kalınır.
    /// </summary>
    private void AttachToInputDesktop()
    {
        string desktop = desktops.Current;
        if (desktop == _desktop) return;

        IntPtr handle = Win32.OpenInputDesktop(0, false, Win32.GENERIC_ALL);
        if (handle == IntPtr.Zero)
        {
            if (_desktop.Length == 0) _desktop = desktop;   // ilk kez: varsayılan masaüstünde kalınır
            return;
        }

        ReleasePressed($"masaüstü değişti: {(_desktop.Length > 0 ? _desktop : "?")} → {desktop}");
        if (!Win32.SetThreadDesktop(handle))
        {
            logger.LogWarning("Uzaktan kontrol: {Desktop} masaüstüne geçilemedi (hata {Error})", desktop, Marshal.GetLastPInvokeError());
            Win32.CloseDesktop(handle);
            return;
        }

        if (_desktopHandle != IntPtr.Zero) Win32.CloseDesktop(_desktopHandle);
        _desktopHandle = handle;
        _desktop = desktop;
        logger.LogInformation("Uzaktan kontrol: girdi {Desktop} masaüstüne gönderiliyor", desktop);
    }

    private void Process(List<Work> works)
    {
        Guid? active;
        lock (_gate) active = _active?.ControlSessionId;
        _turnSent = _turnSendFailed = _turnMonitorMissing = false;
        bool evaluate = false;

        // Düz liste: (monitör, olay). Kontrol oturumu artık aktif değilse paket atılır.
        var items = new List<(int Monitor, InputEvent Event)>();
        foreach (var work in works)
        {
            evaluate |= work.Evaluate || work.RefreshMonitors;
            if (work.RefreshMonitors)
                LoadMonitors();
            if (work.ReleaseAll)
            {
                Flush(items);
                ReleasePressed("kontrol bitti");
                continue;
            }
            if (work.Batch is { } batch && batch.ControlSessionId == active)
                items.AddRange(batch.Events.Select(e => (batch.MonitorIndex, e)));
        }
        Flush(items);
        ReportInputState(active, evaluate);
    }

    /// <summary>
    /// Turun sonucundan girdi durumunu çıkarır ve değiştiyse bildirir. Gönderim ya da değerlendirme yoksa (yalnızca bırakma turu) durum aynı
    /// kalır. Red bir sonraki başarılı turda kendiliğinden kalkar.
    /// </summary>
    private void ReportInputState(Guid? active, bool evaluate)
    {
        if (active is not { } sessionId)
            return;
        if (_stateSessionId != sessionId)
        {
            _stateSessionId = sessionId;
            _state = ControlInputState.Ok;   // merkez de oturumu Ok varsayımıyla başlatır
        }

        ControlInputState? next;
        if (_turnMonitorMissing)
            next = ControlInputState.MonitorMissing;
        else if (_turnSendFailed)
            next = CanOpenInputDesktop() ? ControlInputState.Rejected : ControlInputState.SecureDesktopUnavailable;
        else if (_turnSent || evaluate)
        {
            string current = desktops.Current;
            next = current == InputDesktopWatcher.Default ? ControlInputState.Ok
                : current == _desktop ? ControlInputState.SecureDesktop
                : ControlInputState.SecureDesktopUnavailable;
        }
        else
            next = null;

        if (next is not { } state || state == _state)
            return;

        _state = state;
        logger.LogInformation("Uzaktan kontrol girdisi: {State} (masaüstü {Desktop}, oturum {ControlSessionId})",
            state, _desktop.Length > 0 ? _desktop : InputDesktopWatcher.Default, sessionId);
        try { InputStateChanged?.Invoke(new ControlInputReport(sessionId, state)); }
        catch (Exception ex) { logger.LogWarning(ex, "Uzaktan kontrol girdi durumu iletilemedi"); }
    }

    /// <summary> Giriş masaüstü bu süreçten açılabiliyor mu — reddin nedenini ayırmak için, yalnızca bir gönderim reddedilince sorulur. </summary>
    private static bool CanOpenInputDesktop()
    {
        IntPtr desktop = Win32.OpenInputDesktop(0, false, Win32.DESKTOP_READOBJECTS);
        if (desktop == IntPtr.Zero) return false;
        Win32.CloseDesktop(desktop);
        return true;
    }

    private void Flush(List<(int Monitor, InputEvent Event)> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var (monitorIndex, e) = items[i];
            // Son-durum: sonraki olay da hareketse bu hareket atlanır.
            if (e.Type == InputEventType.Move && i + 1 < items.Count && items[i + 1].Event.Type == InputEventType.Move)
                continue;
            Apply(monitorIndex, e);
        }
        items.Clear();
    }

    private void Apply(int monitorIndex, InputEvent e)
    {
        _lastEventUtc = DateTime.UtcNow;
        switch (e.Type)
        {
            case InputEventType.Move when e.X is { } x && e.Y is { } y:
                if (ToVirtualDesk(monitorIndex, x, y) is { } p)
                    Send(Mouse(p.X, p.Y, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK));
                break;

            case InputEventType.Down or InputEventType.Up when e.Button is { } button:
                bool down = e.Type == InputEventType.Down;
                uint flags = ButtonFlag(button, down);
                int dx = 0, dy = 0;
                if (e.X is { } bx && e.Y is { } by && ToVirtualDesk(monitorIndex, bx, by) is { } bp)
                {
                    (dx, dy) = bp;
                    flags |= MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
                }
                if (Send(Mouse(dx, dy, 0, flags)))
                {
                    if (down) _pressed.Add(button);
                    else _pressed.Remove(button);
                }
                break;

            case InputEventType.KeyDown or InputEventType.KeyUp when WindowsScanCodes.TryGet(e.Code, out ushort scan, out bool extended):
                bool keyDown = e.Type == InputEventType.KeyDown;
                if (Send(Key(scan, extended, keyDown)))
                {
                    if (keyDown) _pressedKeys.Add(e.Code!);
                    else _pressedKeys.Remove(e.Code!);
                }
                break;

            case InputEventType.Wheel:
                // DOM: pozitif deltaY = aşağı; Windows: pozitif = ileri (yukarı). Yatayda ikisi de sağa pozitif.
                if (e.DeltaY is { } wy and not 0)
                    Send(Mouse(0, 0, unchecked((uint)-wy), MOUSEEVENTF_WHEEL));
                if (e.DeltaX is { } wx and not 0)
                    Send(Mouse(0, 0, unchecked((uint)wx), MOUSEEVENTF_HWHEEL));
                break;
        }
    }

    private void ReleasePressed(string reason)
    {
        if (_pressed.Count == 0 && _pressedKeys.Count == 0) return;
        foreach (var button in _pressed)
            Send(Mouse(0, 0, 0, ButtonFlag(button, down: false)));
        foreach (var code in _pressedKeys)
            if (WindowsScanCodes.TryGet(code, out ushort scan, out bool extended))
                Send(Key(scan, extended, down: false));
        logger.LogInformation("Basılı kalanlar bırakıldı ({Reason}): {Buttons} {Keys}", reason, string.Join(", ", _pressed), string.Join(", ", _pressedKeys));
        _pressed.Clear();
        _pressedKeys.Clear();
    }

    private void LoadMonitors()
    {
        try { _monitors = monitors.GetMonitors(); }
        catch (Exception ex) { logger.LogWarning(ex, "Uzaktan kontrol: monitörler okunamadı"); }
    }

    /// <summary> [0,1] → sanal masaüstünde 0–65535. Monitör yoksa (çıkarılmış) <c>null</c>. </summary>
    private (int X, int Y)? ToVirtualDesk(int monitorIndex, double x, double y)
    {
        var monitor = _monitors.FirstOrDefault(m => m.Index == monitorIndex);
        if (monitor is null)
        {
            LoadMonitors();   // ekran düzeni değişmiş olabilir
            monitor = _monitors.FirstOrDefault(m => m.Index == monitorIndex);
            if (monitor is null)
            {
                _turnMonitorMissing = true;
                return null;
            }
        }

        int left = _monitors.Min(m => m.Left), top = _monitors.Min(m => m.Top);
        int right = _monitors.Max(m => m.Left + m.Width), bottom = _monitors.Max(m => m.Top + m.Height);

        double px = monitor.Left + Math.Clamp(x, 0, 1) * (monitor.Width - 1);
        double py = monitor.Top + Math.Clamp(y, 0, 1) * (monitor.Height - 1);
        return ((int)Math.Round((px - left) * 65535.0 / Math.Max(1, right - left - 1)),
                (int)Math.Round((py - top) * 65535.0 / Math.Max(1, bottom - top - 1)));
    }

    private static uint ButtonFlag(MouseButton button, bool down) => (button, down) switch
    {
        (MouseButton.Left, true) => MOUSEEVENTF_LEFTDOWN,
        (MouseButton.Left, false) => MOUSEEVENTF_LEFTUP,
        (MouseButton.Right, true) => MOUSEEVENTF_RIGHTDOWN,
        (MouseButton.Right, false) => MOUSEEVENTF_RIGHTUP,
        (MouseButton.Middle, true) => MOUSEEVENTF_MIDDLEDOWN,
        _ => MOUSEEVENTF_MIDDLEUP,
    };

    private static INPUT Mouse(int dx, int dy, uint data, uint flags) =>
        new() { Type = INPUT_MOUSE, U = new InputUnion { Mi = new MOUSEINPUT { Dx = dx, Dy = dy, MouseData = data, Flags = flags } } };

    private static INPUT Key(ushort scanCode, bool extended, bool down) =>
        new()
        {
            Type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    Scan = scanCode,
                    Flags = KEYEVENTF_SCANCODE | (extended ? KEYEVENTF_EXTENDEDKEY : 0) | (down ? 0 : KEYEVENTF_KEYUP)
                }
            }
        };

    private unsafe bool Send(INPUT input)
    {
        if (SendInput(1, &input, sizeof(INPUT)) == 1)
        {
            _turnSent = true;
            return true;
        }

        // UIPI (yönetici penceresi — yalnızca Agent SYSTEM değilken), erişilemeyen güvenli masaüstü, güvenlik yazılımı: Windows reddeder ve
        // nedenini söylemez (SendInput belgesi). Durum turun sonunda hesaplanıp değişince bildirilir ve loglanır (ReportInputState).
        _turnSendFailed = true;
        logger.LogDebug("Uzaktan kontrol: {Desktop} masaüstüne girdi reddedildi (hata {Error})", _desktop, Marshal.GetLastPInvokeError());
        return false;
    }

    private readonly record struct Work(InputBatch? Batch, bool ReleaseAll = false, bool RefreshMonitors = false, bool Evaluate = false);

    #region Win32
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    // INPUT = tür + union (MOUSEINPUT | KEYBDINPUT). Union'ın en büyük üyesi MOUSEINPUT: x64'te INPUT 40 bayt (sizeof ile gönderilir).
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mi;
        [FieldOffset(0)] public KEYBDINPUT Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static unsafe partial uint SendInput(uint cInputs, INPUT* pInputs, int cbSize);
    #endregion
}
