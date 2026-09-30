namespace Scadex.RemoteDesk.Windows.Services.Shell;

/// <summary>
/// System Tray ikonu (WinForms <c>NotifyIcon</c>, üçüncü parti paket yok. Pencere kapatılınca uygulama buraya iner; çıkış yalnızca menüden. UI iş parçacığında oluşturulmalı ve güncellenmelidir.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public TrayIcon(Action show, Action reconnect, Action exit)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => show());
        menu.Items.Add("Yeniden bağlan", null, (_, _) => reconnect());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => exit());

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
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

    public void Dispose()
    {
        // Görünür bırakılırsa süreç bittikten sonra fare üstüne gelene kadar System Tray'de "hayalet" ikon kalır.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
