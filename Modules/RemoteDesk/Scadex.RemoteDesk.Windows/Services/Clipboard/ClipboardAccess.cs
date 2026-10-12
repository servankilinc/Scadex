using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace Scadex.RemoteDesk.Windows.Services.Clipboard;

/// <summary>
/// Windows panosunu okur/yazar (panolar arası aktarım). WPF panosu STA ister; işlem UI dispatcher'ında yapılır.
/// Pano başka süreçte kilitliyse <c>Clipboard.SetText</c> kendi içinde yeniden dener; yine de hata olursa yutulur.
/// </summary>
public interface IClipboardAccess
{
    /// <summary> Panodaki metin (yoksa <c>null</c>). </summary>
    Task<string?> ReadAsync();

    /// <summary> Metni panoya yazar (boşsa panoyu temizler). </summary>
    Task WriteAsync(string text);
}

public sealed class ClipboardAccess : IClipboardAccess
{
    private readonly ILogger<ClipboardAccess> _logger;

    public ClipboardAccess(ILogger<ClipboardAccess> logger) => _logger = logger;

    /// <summary> Metni ~1.000.000 karaktere sınırlar: hub mesaj sınırını aşan devasa uzak pano bağlantıyı koparmasın. </summary>
    private const int MaxTextLength = 1_000_000;

    public Task<string?> ReadAsync() => OnUiAsync(() =>
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
                return null;
            string text = System.Windows.Clipboard.GetText();
            return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pano okunamadı");
            return null;
        }
    });

    public Task WriteAsync(string text) => OnUiAsync<object?>(() =>
    {
        try
        {
            if (string.IsNullOrEmpty(text))
                System.Windows.Clipboard.Clear();
            else
                System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Panoya yazılamadı");
        }
        return null;
    });

    /// <summary> İşi WPF UI (STA) thread'inde çalıştırır; hub çağrıları thread havuzundan geldiği için dispatcher'a geçer. </summary>
    private static Task<T> OnUiAsync<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            return dispatcher.InvokeAsync(func).Task;
        return Task.FromResult(func());
    }
}
