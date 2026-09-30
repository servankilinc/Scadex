using System.Runtime.InteropServices;

namespace Scadex.RemoteDesk.Windows.Services.Shell;

/// <summary>
/// System Tray ikonu (WinForms <c>NotifyIcon</c>, üçüncü parti paket yok). Pencere kapatılınca uygulama buraya iner; çıkış yalnızca menüden.
/// Merkez ekranı izlerken ikon kırmızı noktalı olur. UI iş parçacığında oluşturulmalı ve güncellenmelidir.
/// </summary>
public sealed partial class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _normalIcon = System.Drawing.SystemIcons.Application;
    private readonly System.Drawing.Icon _watchingIcon;
    private readonly IntPtr _watchingHandle;

    public TrayIcon(Action show, Action reconnect, Action exit)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => show());
        menu.Items.Add("Yeniden bağlan", null, (_, _) => reconnect());
        menu.Items.Add("Günlük klasörünü aç", null, (_, _) => ClientLog.OpenFolder());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => exit());

        (_watchingIcon, _watchingHandle) = CreateWatchingIcon(_normalIcon);

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _normalIcon,
            Text = "Scadex RemoteDesk",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => show();
    }

    /// <summary> İpucu metni (Windows sınırı 63 karakter). </summary>
    public void SetStatus(string text)
    {
        string full = "Scadex RemoteDesk — " + text;
        _icon.Text = full.Length <= 63 ? full : full[..62] + "…";
    }

    public void SetWatching(bool watching) => _icon.Icon = watching ? _watchingIcon : _normalIcon;

    public void Dispose()
    {
        // Görünür bırakılırsa süreç bittikten sonra fare üstüne gelene kadar System Tray'de "hayalet" ikon kalır.
        _icon.Visible = false;
        _icon.Dispose();
        _watchingIcon.Dispose();
        DestroyIcon(_watchingHandle);
    }

    /// <summary> Uygulama ikonunun sağ altına kırmızı nokta — ayrı bir .ico dosyası taşımamak için çalışırken çizilir. </summary>
    private static (System.Drawing.Icon Icon, IntPtr Handle) CreateWatchingIcon(System.Drawing.Icon baseIcon)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawIcon(baseIcon, new System.Drawing.Rectangle(0, 0, 32, 32));
            g.FillEllipse(System.Drawing.Brushes.White, 13, 13, 19, 19);
            using var red = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xC5, 0x22, 0x1F));
            g.FillEllipse(red, 15, 15, 15, 15);
        }
        IntPtr handle = bitmap.GetHicon();
        return (System.Drawing.Icon.FromHandle(handle), handle);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);
}
