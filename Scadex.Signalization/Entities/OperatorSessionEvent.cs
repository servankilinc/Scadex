using Scadex.Core.Model;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Oturumun zaman cizelgesi. Kapi acilis/kapanis/kilit zamanlari BURADAN kurulur — kapi basina kolon tutulmaz,
/// cunku ayni kapi bir oturumda birden fazla kez acilabilir. Tek guncelleme: siren talep olayina, uzlastirma
/// komut gonderdiyse o komutun kimligi islenir.
/// </summary>
public class OperatorSessionEvent : IEntity
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public SessionEventType Type { get; set; }

    /// <summary> Olayin gerceklestigi an: SCADA kaynakliysa sahadaki zaman, motor kaynakliysa sunucu zamani. </summary>
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }

    public Guid? InnerDoorId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary> Ham kart kimligi — kart tanimsiz olsa da yazilir; guvenlik incelemesinin en cok ihtiyac duydugu veri. </summary>
    public string? CardIdRaw { get; set; }

    /// <summary> Olay bir komut urettiyse <c>DeviceCommand.Id</c> — FK DEGIL. </summary>
    public Guid? DeviceCommandId { get; set; }

    /// <summary> Olay bir kare cekimiyse <c>CameraCapture.Id</c> — FK DEGIL. </summary>
    public long? CameraCaptureId { get; set; }

    /// <summary> Gerekce anahtari (<see cref="Enums.SessionEventDetail"/>) ya da kisa aciklama. </summary>
    public string? Detail { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
