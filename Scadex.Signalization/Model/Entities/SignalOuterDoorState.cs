using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary>
/// Dış kapı aydınlatmasının son bilinen durumu; SCADA output durumlarını geri vermediği için gönderdiğimiz
/// komutu biz takip ediyoruz. Kayıt yoksa LED sönük sayılır — bu olmadan her kapı açılışında aynı komut
/// tekrar giderdi.
/// </summary>
public class SignalOuterDoorState : IEntity
{
    public Guid OuterDoorId { get; set; }
    public bool LightIsOn { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    /// <summary> Son aydınlatma komutunun <c>DeviceCommand.Id</c>'si </summary>
    public Guid? LastCommandId { get; set; }
}
