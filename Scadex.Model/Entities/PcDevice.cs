using Scadex.Core.Model;

namespace Scadex.Model.Entities;

/// <summary>
/// Bilgisayarın (RemoteDesk) ek alanları — <see cref="Device"/> ile 1-1, ortak birincil anahtar.
/// Şablon tipi <c>DeviceType.Pc</c> olan her cihazın bir satırı vardır. Tek yazım yolu diyagram deltasıdır (<c>DeviceDraft.Pc</c>);
/// RemoteDesk modülü bu tabloyu yalnızca projeksiyonla okur.
/// </summary>
public class PcDevice : IEntity
{
    public Guid DeviceId { get; set; }

    /// <summary>
    /// RemoteDesk istemcisinin <c>PcHub</c>'a Basic Auth ile bağlanma kimliği: kullanıcı adı <see cref="DeviceId"/>, parola sistemin
    /// ürettiği sır. Burada sırrın SHA-256'sı durur (<c>DeviceCredential</c>); <c>null</c> = kimlik bilgisi yok, istemci bağlanamaz.
    /// </summary>
    public string? CredentialHash { get; set; }

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
