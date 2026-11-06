using Scadex.Core.Model;
using static Scadex.CameraManagement.Model.Enums.CameraManagementEnums;

namespace Scadex.CameraManagement.Model.Entities;

/// <summary> Merkeze alınmış TEK bir görüntü klip kaydı. </summary>
public class CameraCapture : IEntity
{
    public long Id { get; set; }

    /// <summary> Çekirdekteki kamera (<c>Camera.DeviceId</c>). FK DEĞİLDİR (modül kuralı); bütünlük servisin doğrulamasıyla sağlanır. </summary>
    public Guid CameraId { get; set; }
    public CaptureType Type { get; set; }
    public CaptureStatus Status { get; set; }
    public DateTime CapturedAtUtc { get; set; }

    /// <summary> Klip süresi (saniye); anlık görüntüde <c>null</c>. </summary>
    public int? DurationSec { get; set; }

    /// <summary> Dosyanın depo anahtarı <c>wwwroot</c> altında (örn: <c>uploads/captures/2026/08/27/{guid}.jpg</c>). </summary>
    public string? RelativePath { get; set; }
    public long? SizeBytes { get; set; }
    /// <summary> <see cref="CaptureStatus.Failed"/> ise sebep açıklaması </summary>
    public string? FailureReason { get; set; }
    /// <summary> Saklama süresinin sonu (UTC); <c>null</c> ise süresiz. </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary> Çekimi isteyen kullanıcı (çekirdek <c>User.Id</c>, FK değil); arka plan işinde <c>null</c>. </summary>
    public Guid? RequestedByUserId { get; set; }
}
