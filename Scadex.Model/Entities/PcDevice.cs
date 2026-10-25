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
    public string? MacAddress { get; set; }

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    #endregion
}
