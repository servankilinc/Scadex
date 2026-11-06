using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Config.Queries;

/// <summary> Sinyalizasyon kabinin O ANKI durumu. <see cref="SignalCabinetDto"/> gibi kanal secimlerini duzenlemek icin degil, sahayi izlemek icindir </summary>
/// <remarks>
/// <list type="bullet">
/// <item> <strong>Inputs</strong> ("kapı açık mı"): SCADA'nin ingest ettigi ham deger, kapinin <c>SwitchOpenValue</c>'suyla yorumlanir. </item>
/// <item> <strong>Outputs</strong> ("siren", "aydınlatma", "kilit"): son BASARILI komutun degeri, NO/NC kablolamasina gore yorumlanir </item>
/// </list>
/// </remarks>
public class SignalCabinetLiveDto : IDto
{
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }

    /// <summary> Kabin hic yapilandirilmadiysa <c>false</c> ve agac bostur. </summary>
    public bool IsConfigured { get; set; }

    /// <summary> <c>false</c> ise motor bu kabinin olaylarini yok sayar; ekran bunu uyari olarak gosterir. </summary>
    public bool IsEnabled { get; set; }

    public Guid? SirenIoChannelId { get; set; }

    /// <summary> Siren caliyor mu; siren tanimsiz ya da hic basarili komut gormemisse <c>null</c>. </summary>
    public bool? SirenIsOn { get; set; }

    /// <summary> Sirenin bu duruma gectigi an; bilinmiyorsa <c>null</c>. </summary>
    public DateTime? SirenChangedAtUtc { get; set; }

    public List<SignalOuterDoorLiveDto> OuterDoors { get; set; } = [];
}

public class SignalOuterDoorLiveDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public Guid? CameraId { get; set; }
    public string? CameraName { get; set; }

    public Guid SwitchIoChannelId { get; set; }

    /// <summary> Switch'in "kapı açık" anlamına gelen değeri (bilgi amacli; yorum sunucuda yapilir). </summary>
    public string SwitchOpenValue { get; set; } = null!;

    /// <summary> Kapı açık mı. Kanal pasif ya da hic deger okunmadiysa <c>null</c> ("bilinmiyor"). Canlı değişim <c>SignalDoorSwitchChanged</c> yaynıyla gelir </summary>
    public bool? IsOpen { get; set; }

    /// <summary> Anahtar kanalinin son deger degisimi (<c>IoChannel.ValueUpdatedAt</c>). </summary>
    public DateTime? SwitchChangedAtUtc { get; set; }

    /// <summary> Aydinlatma tanimli degilse <c>null</c> — ekranda LED komutu sunulmaz. </summary>
    public Guid? LightIoChannelId { get; set; }

    /// <summary> LED yaniyor mu; bilinmiyorsa <c>null</c>. </summary>
    public bool? LightIsOn { get; set; }
    public DateTime? LightChangedAtUtc { get; set; }

    public List<SignalInnerDoorLiveDto> InnerDoors { get; set; } = [];
}

public class SignalInnerDoorLiveDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid AuthorityId { get; set; }
    public string? AuthorityName { get; set; }

    public Guid SwitchIoChannelId { get; set; }

    /// <inheritdoc cref="SignalOuterDoorLiveDto.SwitchOpenValue" />
    public string SwitchOpenValue { get; set; } = null!;

    /// <inheritdoc cref="SignalOuterDoorLiveDto.IsOpen" />
    public bool? IsOpen { get; set; }
    public DateTime? SwitchChangedAtUtc { get; set; }

    public Guid LockIoChannelId { get; set; }

    /// <summary> 
    /// Kilit açık mı, kilit kanalının son başarılı komutudur; bilinmiyorsa <c>null</c>.
    /// Kapi acik (<see cref="IsOpen"/>) + kilit kilitli (<c>false</c>) zorlanmis acilistir.
    /// </summary>
    public bool? IsUnlocked { get; set; }
    public DateTime? LockChangedAtUtc { get; set; }
}
