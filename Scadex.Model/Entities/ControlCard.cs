using Scadex.Core.Model;

namespace Scadex.Model.Entities;

/// <summary>
/// Kontrol modülünün (SCADA kartı) ek alanları — <see cref="Device"/> ile 1-1, ortak birincil anahtar.
/// Tek yazım yolu diyagramın cihaz uçlarıdır (<c>DeviceCreateRequest</c> / <c>DeviceUpdateRequest</c>.<c>ControlCard</c>).
/// </summary>
public class ControlCard : IEntity
{
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Kartın MAC adresi, tek tip <c>AA:BB:CC:DD:EE:FF</c> (<c>MacAddressFormat</c>). SCADA Merkeze (ingest, kart okuma) her istekte bunu
    /// <c>DeviceId</c> header'ında gönderir; kart ve kabini buradan çözülür. <c>null</c> = kart bağlanamaz.
    /// Tekillik bilerek denetlenmez (2026-10-28, kullanıcı kararı): aynı MAC iki aktif kartta varsa ilk eşleşen kazanır.
    /// </summary>
    public string? MacAddress { get; set; }

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
