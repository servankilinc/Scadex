using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Kabin sireninin FIZIKSEL durumu. Calisma durumudur: yalnizca motor yazar, yapilandirma PUT'u dokunmaz.
/// Oturumlarin siren TALEPLERI ile ayrilir: istenen durum "en az bir acik talep var mi", fiziksel durum bu satir;
/// ikisi farkliysa tek komut gider. Boylece ikinci bir talep ikinci bir "ac" komutu, bir talebin kapanmasi da
/// digeri acikken susturma uretmez.
/// </summary>
public class SignalCabinetState : IEntity
{
    public Guid CabinetId { get; set; }
    public bool SirenIsOn { get; set; }
    public DateTime? SirenChangedAtUtc { get; set; }

    /// <summary> Son siren komutunun <c>DeviceCommand.Id</c>'si — FK DEGIL. </summary>
    public Guid? LastSirenCommandId { get; set; }
}
