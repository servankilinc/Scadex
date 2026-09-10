namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// MediaMTX'teki bir path'in temizlik kararı verebilmek icin gereken hali bilgiler.
/// (<c>v3/paths/list</c>) ile yapilandirmasi (<c>v3/config/paths/list</c>) birleştirilerek oluşur.
/// </summary>
/// <param name="Name">Yol adi.</param>
/// <param name="IsConfigured">Yapilandirmada var mi? Yoksa silinecek bir sey de yoktur.</param>
/// <param name="ReaderCount">O anki izleyici sayisi. &gt; 0 ise yol KULLANIMDADIR.</param>
/// <param name="RecordEnabled">Yapilandirmasinda <c>record: true</c> mi? Klip cekimleri boyle isaretlidir.</param>
/// <param name="IsReady">Kaynaga baglanmis ve yayin veriyor mu?</param>
public readonly record struct MediaPathInfo(
    string Name,
    bool IsConfigured,
    int ReaderCount,
    bool RecordEnabled,
    bool IsReady
);
