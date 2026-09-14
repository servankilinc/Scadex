using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary>
/// Kabin son bilinen output durumları, Şu an siren için kullanıyoruz ilerde genişletilebilir, scada output durumlarını vermiyor o yüzden biz takip ediyoruz.
/// </summary>
public class SignalCabinetState : IEntity
{
    public Guid CabinetId { get; set; }
    public bool SirenIsOn { get; set; }
    public DateTime? SirenChangedAtUtc { get; set; }

    /// <summary> Son siren komutunun <c>DeviceCommand.Id</c>'si </summary>
    public Guid? LastSirenCommandId { get; set; }
}
