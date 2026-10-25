using Scadex.RemoteDesk.Contracts.Hub;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Dtos.Control;

/// <summary> Viewer hub <c>RequestControl</c> yanıtı. Red de bir yanıttır (istisna değil): tarayıcı nedeni gösterir. </summary>
public class RequestControlResponse
{
    public ControlRequestStatus Status { get; set; }

    /// <summary> Yalnızca <c>Granted</c>'da; girdi paketleri (<c>InputBatch.ControlSessionId</c>) bunu taşır. </summary>
    public Guid? ControlSessionId { get; set; }

    /// <summary> <c>Busy</c>'de kontrol edenin adı. </summary>
    public string? ControllerName { get; set; }

    public string? Message { get; set; }

    /// <summary> Bu kadar saniye girdi gelmezse kontrol düşer. </summary>
    public int IdleTimeoutSec { get; set; }
}

/// <summary> Sunucu → tarayıcı: kontrol sunucu tarafında bitti (boşta kaldı, PC koptu, izleme bitti, başka sekme aldı). </summary>
public class ControlEndedNotice
{
    public Guid ControlSessionId { get; set; }
    public RemoteControlEndReason Reason { get; set; }
}

/// <summary>
/// Sunucu → tarayıcı: kontrol edilen PC'de girdinin durumu değişti. Yalnızca kontrol eden tarayıcıya, yalnızca değişince gider; kontrol <c>Ok</c> varsayımıyla başlar.
/// </summary>
public class ControlInputNotice
{
    public Guid ControlSessionId { get; set; }
    public ControlInputState State { get; set; }
}

/// <summary> PC ekranı: şu an kimin kontrol ettiği. </summary>
public class PcControlDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public DateTime StartedUtc { get; set; }
}
