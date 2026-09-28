using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Model.Dtos.Session.Queries;

public class OperatorSessionListItemDto : IDto
{
    public long Id { get; set; }
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }
    public Guid OuterDoorId { get; set; }
    public string OuterDoorName { get; set; } = null!;
    public OperatorSessionStatus Status { get; set; }
    public SessionFlags Flags { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSec { get; set; }
    public int CaptureCount { get; set; }

    /// <summary> Oturumda guvenlik uyarisi flag var mı. </summary>
    public bool HasAlert { get; set; }
    public List<OperatorSessionOperatorDto> Operators { get; set; } = [];
}

public class OperatorSessionOperatorDto : IDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string AuthorityName { get; set; } = null!;
    public string CardIdRaw { get; set; } = null!;
    public DateTime FirstCardAtUtc { get; set; }
    public DateTime LastCardAtUtc { get; set; }
}


/// <summary> Canlı panelin için yalnızca açık oturumlar. </summary>
public class OperatorSessionOpenDto : OperatorSessionListItemDto
{
    public SessionPhase Phase { get; set; }
    public int ElapsedSec { get; set; }
    public bool SirenRequested { get; set; }
    public bool CabinetSirenIsOn { get; set; }
}

public class OperatorSessionDetailDto : OperatorSessionListItemDto
{
    public DateTime? SirenRequestedAtUtc { get; set; }
    public DateTime? SirenReleasedAtUtc { get; set; }
    public List<OperatorSessionEventDto> Events { get; set; } = [];
    public List<OperatorSessionDoorDto> Doors { get; set; } = [];
    public List<OperatorSessionCaptureDto> Captures { get; set; } = [];
}

public class OperatorSessionEventDto : IDto
{
    public long Id { get; set; }
    public SessionEventType Type { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public Guid? InnerDoorId { get; set; }
    public string? InnerDoorName { get; set; }
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? CardIdRaw { get; set; }
    public Guid? DeviceCommandId { get; set; }
    public long? CameraCaptureId { get; set; }
    public string? Detail { get; set; }
}

/// <summary>
/// Kabinin kart okuyucusundaki tek okuma — "son okumalar" listesi. <c>CardPresented</c> ve <c>AccessDenied</c>
/// olaylarindan turetilir; acik oturum yokken reddedilen kart olay olarak yazilmadigi icin burada gorunmez.
/// </summary>
public class SignalCardReadDto : IDto
{
    /// <summary> Olayin Id'si (<c>OperatorSessionEvent.Id</c>). </summary>
    public long Id { get; set; }
    public long SessionId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? CardIdRaw { get; set; }

    /// <summary> Kart kabul edildi mi (<c>CardPresented</c>); <c>false</c> ise reddedildi (<c>AccessDenied</c>), gerekce <see cref="Detail"/>'dadir. </summary>
    public bool IsAccepted { get; set; }

    /// <summary> Kartin sahibi; tanimsiz kartta <c>null</c>. </summary>
    public Guid? UserId { get; set; }

    /// <summary> Once oturumdaki enstantane, yoksa (reddedilen kullanici) cekirdekteki bugunku ad. </summary>
    public string? UserFullName { get; set; }

    /// <summary> Yalnizca kabul edilen okumada: operatorun oturuma katildigi kurum. </summary>
    public string? AuthorityName { get; set; }

    /// <summary> Yalnizca kabul edilen okumada: kartin actigi / kilitledigi ic kapi. </summary>
    public string? InnerDoorName { get; set; }

    /// <summary> Red gerekcesi (<c>SessionEventDetail</c> anahtari) ya da kabulde ortuk oturum notu. </summary>
    public string? Detail { get; set; }
}

/// <summary> Ic kapinin oturumdaki ozeti — olaylardan turetilir. </summary>
public class OperatorSessionDoorDto : IDto
{
    public Guid InnerDoorId { get; set; }
    public string Name { get; set; } = null!;
    public string? AuthorityName { get; set; }
    public DateTime? FirstUnlockedAtUtc { get; set; }
    public DateTime? FirstOpenedAtUtc { get; set; }
    public DateTime? LastClosedAtUtc { get; set; }
    public DateTime? LastLockedAtUtc { get; set; }
    public int OpenCount { get; set; }
    public bool WasForcedOpen { get; set; }
}

public class OperatorSessionCaptureDto : IDto
{
    public long CameraCaptureId { get; set; }
    public int Sequence { get; set; }
    public CaptureStatus? Status { get; set; }
    public DateTime? CapturedAtUtc { get; set; }
    public string? RelativePath { get; set; }
    public string? FailureReason { get; set; }
}
