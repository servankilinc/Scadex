using Scadex.Core.Model;
using Scadex.Signalization.Model.Dtos.Authority.Queries;

namespace Scadex.Signalization.Model.Dtos.Config.Queries;

public class SignalCabinetDto : IDto
{
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }
    
    /// <summary> Kabin hic yapılandırılmadıysa <c>false</c> ve model varsayilanlar bilgilerle döner. </summary>
    public bool IsConfigured { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? SirenIoChannelId { get; set; }
    public int SirenDurationSec { get; set; }
    public int EntrySnapshotCount { get; set; }
    public int EntrySnapshotIntervalMs { get; set; }
    public int AwaitingCardTimeoutSec { get; set; }
    public int SessionMaxDurationMin { get; set; }

    /// <summary> Sirenin fiziksel durumu. </summary>
    public bool SirenIsOn { get; set; }

    public List<SignalOuterDoorDto> OuterDoors { get; set; } = [];
}

public class SignalOuterDoorDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid SwitchIoChannelId { get; set; }
    public string SwitchOpenValue { get; set; } = null!;
    public Guid? CameraId { get; set; }

    /// <summary> Kapının aydınlatma LED'inin output kanalı; </summary>
    public Guid? LightIoChannelId { get; set; }
    public List<SignalInnerDoorDto> InnerDoors { get; set; } = [];
}

public class SignalInnerDoorDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid AuthorityId { get; set; }
    public Guid SwitchIoChannelId { get; set; }
    public string SwitchOpenValue { get; set; } = null!;
    public Guid LockIoChannelId { get; set; }
    public bool UnlockTurnsOn { get; set; }

    /// <summary> Kilidin son bilinen durumu. </summary>
    public bool IsUnlocked { get; set; }
}

/// <summary> Yapilandirma ekraninin seçenekleri. </summary>
public class SignalCabinetOptionsDto : IDto
{
    public List<SignalChannelOptionDto> InputChannels { get; set; } = [];
    public List<SignalChannelOptionDto> OutputChannels { get; set; } = [];
    public List<SignalCameraOptionDto> Cameras { get; set; } = [];
    public List<SignalAuthorityDto> Authorities { get; set; } = [];
}

public class SignalChannelOptionDto : IDto
{
    public Guid Id { get; set; }
    public int ChannelNumber { get; set; }

    /// <summary> SCADA adresi: <c>IN3</c>, <c>OUT1</c>. </summary>
    public string Address { get; set; } = null!;
    public string ChannelName { get; set; } = null!;

    /// <summary> Kanalin karti </summary>
    public string? DeviceName { get; set; }

    /// <summary> Kanalın pinine bagli saha cihazının adı (orn. "Emniyet Kapi Switch"). </summary>
    public string? WiredDeviceName { get; set; }
    public string? CurrentValue { get; set; }

    /// <summary> Kanal başka bir kapıda kullaniliyorsa o kapinin adi. </summary>
    public string? UsedBy { get; set; }
}

public class SignalCameraOptionDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}
