using Scadex.Core.Model;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Model.Entities;

/// <summary> İşlem oturumunun zaman çizelgesi. Kapı açılış/kapanış/kilit vb zamanları. </summary>
public class OperatorSessionEvent : IEntity
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public SessionEventType Type { get; set; }

    /// <summary> Scada'da oluştuysa scada'dan gelen veya Scadex tarafından oluşturulma zamanı </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary> Olayın bizim işlenmeye başlandığı uaştığı zaman(Asıl amaç Scada tarafından olay oluşma zamanı ile kıyaslamak için ekledim)</summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary> Hangi iç kapıda gerçekleşti </summary>
    public Guid? InnerDoorId { get; set; }

    /// <summary> Hangi operatör yaptı </summary>
    public Guid? UserId { get; set; }

    /// <summary> Ham operatör kimligi, tanımsız olsa da yazılır güvenlik kontrolü için </summary>
    public string? CardIdRaw { get; set; }

    /// <summary> Olay bir komut urettiyse <c>DeviceCommand.Id</c> </summary>
    public Guid? DeviceCommandId { get; set; }

    /// <summary> Olay bir kare cekimiyse <c>CameraCapture.Id</c>  </summary>
    public long? CameraCaptureId { get; set; }

    /// <summary> Gerekce sistem tarafından atanacak <see cref="Enums.SessionEventDetail"/> ya da kısa açıklama </summary>
    public string? Detail { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
