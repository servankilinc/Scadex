using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Monitors;

namespace Scadex.RemoteDesk.Windows.Services.Input;

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
}

/// <summary>
/// Merkezden gelen fare ve klavye olaylarını <c>SendInput</c> ile uygular (RemoteDesk.md § 12).
/// <list type="bullet">
/// <item>Tek işçi, tek kuyruk: olay sırası korunur. Ardışık <c>Move</c>'lardan yalnızca sonuncusu uygulanır (son-durum); düğme, tekerlek ve
/// tuş olayları asla düşmez.</item>
/// <item>Tuşlar <c>KEYEVENTF_SCANCODE</c> ile basılır (<see cref="KeyboardScanCodes"/>): karakteri PC'nin kendi klavye düzeni belirler
/// (Türkçe Q'da ğ/ş/ı doğru çıkar).</item>
/// <item>Koordinat: [0,1] → monitörün fiziksel dikdörtgeni (DXGI) → sanal masaüstü 0–65535 (<c>ABSOLUTE | VIRTUALDESK</c>). Sanal masaüstü
/// monitörlerin DXGI dikdörtgenlerinin birleşimidir; <c>GetSystemMetrics</c> kullanılmaz çünkü işlemin DPI farkındalığına göre ölçeklenir.</item>
/// <item>Takılı düğme/tuş emniyeti (§ 12.5): kontrol bitince, bağlantı kopunca ya da basılı bir şey varken 5 sn olay gelmezse basılı düğme ve
/// tuşların hepsi bırakılır (basılı tutulan tuşu tarayıcı tekrarlayarak gönderdiği için sürekli basmak bu süreyi aşmaz).</item>
/// <item>Girdi yalnızca <c>ControlStarted</c> ile <c>ControlEnded</c> arasında, o oturumun kimliğiyle kabul edilir.</item>
/// </list>
/// Normal yetkiyle çalışan istemci yönetici pencerelerine ve kilit ekranına girdi gönderemez (UIPI, § 12.6); Windows bunu sessizce engeller.
/// </summary>
public sealed partial class RemoteInputService(IMonitorService monitors, ILogger<RemoteInputService> logger) : BackgroundService, IRemoteInputService
{
    private static readonly TimeSpan StuckButtonTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(1);

    private readonly Channel<Work> _queue = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Lock _gate = new();
    private ControlStartedCommand? _active;

    // Yalnızca işçi iş parçacığında kullanılır.
    private readonly HashSet<MouseButton> _pressed = [];
    private readonly HashSet<string> _pressedKeys = new(StringComparer.Ordinal);
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private DateTime _lastEventUtc;
    private bool _blockedLogged;

    public string? Controller { get { lock (_gate) return _active?.UserName; } }
    public event Action<string?>? ControllerChanged;

    public void Begin(ControlStartedCommand command)
    {
        lock (_gate) _active = command;
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
        }
        _queue.Writer.TryWrite(new Work(null, ReleaseAll: true));
        if (ended is not null)
        {
            logger.LogInformation("Uzaktan kontrol bitti: merkez bağlantısı koptu (oturum {ControlSessionId})", ended);
            ControllerChanged?.Invoke(null);
        }
    }

    public void Enqueue(InputBatch batch) => _queue.Writer.TryWrite(new Work(batch));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new List<Work>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool hasWork;
                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                {
                    wait.CancelAfter(IdleCheck);
                    try { hasWork = await _queue.Reader.WaitToReadAsync(wait.Token); }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { hasWork = false; }
                }

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
                Process(pending);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            ReleasePressed("uygulama kapanıyor");
        }
    }

    private void Process(List<Work> works)
    {
        Guid? active;
        lock (_gate) active = _active?.ControlSessionId;

        // Düz liste: (monitör, olay). Kontrol oturumu artık aktif değilse paket atılır.
        var items = new List<(int Monitor, InputEvent Event)>();
        foreach (var work in works)
        {
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

            case InputEventType.KeyDown or InputEventType.KeyUp when KeyboardScanCodes.TryGet(e.Code, out ushort scan, out bool extended):
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
            if (KeyboardScanCodes.TryGet(code, out ushort scan, out bool extended))
                Send(Key(scan, extended, down: false));
        logger.LogInformation("Basılı kalanlar bırakıldı ({Reason}): {Buttons} {Keys}", reason, string.Join(", ", _pressed), string.Join(", ", _pressedKeys));
        _pressed.Clear();
        _pressedKeys.Clear();
    }

    private void LoadMonitors()
    {
        try { _monitors = monitors.GetMonitors(); }
        catch (Exception ex) { logger.LogWarning(ex, "Uzaktan kontrol: monitörler okunamadı"); }
        _blockedLogged = false;
    }

    /// <summary> [0,1] → sanal masaüstünde 0–65535. Monitör yoksa (çıkarılmış) <c>null</c>. </summary>
    private (int X, int Y)? ToVirtualDesk(int monitorIndex, double x, double y)
    {
        var monitor = _monitors.FirstOrDefault(m => m.Index == monitorIndex);
        if (monitor is null)
        {
            LoadMonitors();   // ekran düzeni değişmiş olabilir
            monitor = _monitors.FirstOrDefault(m => m.Index == monitorIndex);
            if (monitor is null) return null;
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
            return true;

        // UIPI (yönetici penceresi), kilit ekranı / güvenli masaüstü: Windows reddeder. Oturum başına bir kez loglanır.
        if (!_blockedLogged)
        {
            logger.LogWarning("Uzaktan kontrol: Windows girdiyi reddetti (hata {Error}) — yönetici olarak çalışan pencere, kilit ekranı ya da UAC olabilir",
                Marshal.GetLastPInvokeError());
            _blockedLogged = true;
        }
        return false;
    }

    private readonly record struct Work(InputBatch? Batch, bool ReleaseAll = false, bool RefreshMonitors = false);

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
