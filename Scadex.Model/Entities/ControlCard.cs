using Scadex.Core.Model;

namespace Scadex.Model.Entities;

/// <summary>
/// Kontrol modülünün (SCADA kartı) ek alanları — <see cref="Device"/> ile 1-1, ortak birincil anahtar.
/// Tek yazım yolu diyagramın cihaz uçlarıdır (<c>DeviceCreateRequest</c> / <c>DeviceUpdateRequest</c>.<c>ControlCard</c>).
/// Otomasyon başına en fazla bir aktif kontrol kartı vardır (diyagram doğrulaması zorlar, 2026-11-04): kanal adresleri otomasyon
/// genelinde tekildir çünkü otomasyon tek bir kartın adres uzayıdır.
/// </summary>
public class ControlCard : IEntity
{
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Kartın MAC adresi, tek tip <c>AA:BB:CC:DD:EE:FF</c> (<c>MacAddressFormat</c>). SCADA Merkeze (ingest, kart okuma) her istekte bunu
    /// <c>DeviceId</c> header'ında gönderir; kart ve otomasyonu buradan çözülür. <c>null</c> = kart bağlanamaz.
    /// Tekillik bilerek denetlenmez (2026-10-28, kullanıcı kararı): aynı MAC iki aktif kartta varsa ilk eşleşen kazanır.
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Kartın HTTP adresi: komut <c>POST {ScadaBaseUrl}/command</c>, okuma <c>GET {ScadaBaseUrl}/status</c>. 2026-11-04'e kadar otomasyondaydı.
    /// Değiştirmek kumandayı ve okumayı başka adrese yönlendirir; diyagramın kart formundan <c>EditDiagram</c> ile yazılır (kullanıcı kararı).
    /// </summary>
    public string? ScadaBaseUrl { get; set; }

    /// <summary> Kapalıysa ingest, kart okuma, komut ve karttan okuma reddedilir; kart yoklanmaz. </summary>
    public bool ScadaIsEnabled { get; set; }

    /// <summary> Karta yapılan isteğin zaman aşımı; <c>NoResponse</c>'u <c>Failed</c>'dan ayıran şey. En az 10.000 ms. </summary>
    public int ScadaCommandTimeoutMs { get; set; }

    /// <summary> Son ingest anı (arayüzdeki "son veri"). Telemetridir: yalnızca ingest yazar, cihaz formu dokunmaz. </summary>
    public DateTime? ScadaLastIngestAt { get; set; }

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
