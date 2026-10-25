using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Scadex.RemoteDesk.Windows.Service.Services.Clipboard;

/// <summary>
/// Windows panosunu okur/yazar. Agent penceresizdir: işler kendi <b>STA</b> iş parçacığında, WinForms
/// <c>Clipboard</c> ile yapılır (OLE panosu STA ister; WinForms kilitli panoda kendi içinde yeniden dener). Agent oturumun
/// <c>winsta0</c> pencere istasyonunda çalıştığı için pano oturumdaki kullanıcının panosudur. Hata yutulur ve loglanır; içerik loglanmaz.
/// </summary>
public interface IClipboardAccess
{
    /// <summary> Panodaki metin (yoksa <c>null</c>). </summary>
    Task<string?> ReadAsync();

    /// <summary> Metni panoya yazar (boşsa panoyu temizler). </summary>
    Task WriteAsync(string text);
}

public sealed class ClipboardAccess : IClipboardAccess, IDisposable
{
    /// <summary> Metni ~1.000.000 karaktere sınırlar: hub mesaj sınırını aşan devasa uzak pano bağlantıyı koparmasın. </summary>
    private const int MaxTextLength = 1_000_000;
    /// <summary> Pano başka süreçte uzun süre kilitliyse hub çağrısı sonsuza dek beklemesin. </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<ClipboardAccess> _logger;
    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;

    public ClipboardAccess(ILogger<ClipboardAccess> logger)
    {
        _logger = logger;
        _thread = new Thread(() =>
        {
            foreach (var action in _work.GetConsumingEnumerable())
                action();
        })
        { IsBackground = true, Name = "Pano (STA)" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<string?> ReadAsync() => RunAsync(() =>
    {
        try
        {
            if (!System.Windows.Forms.Clipboard.ContainsText())
                return null;
            string text = System.Windows.Forms.Clipboard.GetText();
            return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pano okunamadı");
            return null;
        }
    });

    public Task WriteAsync(string text) => RunAsync<object?>(() =>
    {
        try
        {
            if (string.IsNullOrEmpty(text))
                System.Windows.Forms.Clipboard.Clear();
            else
                System.Windows.Forms.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Panoya yazılamadı");
        }
        return null;
    });

    private Task<T> RunAsync<T>(Func<T> func)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Add(() => done.TrySetResult(func()));
        return done.Task.WaitAsync(Timeout);
    }

    public void Dispose() => _work.CompleteAdding();
}
