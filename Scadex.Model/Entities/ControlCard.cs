using Scadex.Core.Model;

namespace Scadex.Model.Entities;

/// <summary>
/// Kontrol modülünün (SCADA kartı) ek alanları — <see cref="Device"/> ile 1-1, ortak birincil anahtar.
/// Tek yazım yolu diyagram deltasıdır (<c>DeviceDraft.ControlCard</c>).
/// </summary>
public class ControlCard : IEntity
{
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Kartın Scadex'e (ingest, kart okuma) Basic Auth ile bağlanma kimliği: kullanıcı adı <see cref="DeviceId"/>, parola sistemin ürettiği sır.
    /// Burada sırrın SHA-256'sı durur (<c>DeviceCredential</c>); <c>null</c> = kimlik bilgisi yok, kart bağlanamaz.
    /// Kabin bu kimlikten çözülür. Okuma DTO'larına yazılmaz; yalnızca <c>hasCredential</c> döner.
    /// </summary>
    public string? CredentialHash { get; set; }

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
