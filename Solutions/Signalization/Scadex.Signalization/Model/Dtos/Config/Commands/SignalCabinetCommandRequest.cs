using FluentValidation;
using Scadex.Core.Model;
using Scadex.Signalization.Enums;

namespace Scadex.Signalization.Model.Dtos.Config.Commands;

/// <summary> Sanal kabin ekranından gonderilen manuel komut. Kullanıcı dostu bir dilde konuşur("sireni çal", "kilidi aç" ...) pin vs bilgisi arka planda çözülür </summary>
public class SignalCabinetCommandRequest : IDto
{
    /// <summary> Hangi cihaza komut gönderilecek Siren, Dış Kapı Aydınlatma, İç Kapı Kilit... </summary>
    public SignalCabinetOutput Target { get; set; }

    /// <summary>
    /// Komut göndeirlecek cihazın ID bilgisi <para/>
    /// <see cref="SignalCabinetOutput.Siren"/> ise <c>null</c> (siren kabin başına tek zaten);
    /// <see cref="SignalCabinetOutput.OuterDoorLight"/> ise dış kapı, 
    /// <see cref="SignalCabinetOutput.InnerDoorLock"/> ise iç kapı.
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary> Istenen durum aç/kapat & yak/söndür ... </summary>
    public bool TurnOn { get; set; }
}

public class SignalCabinetCommandRequestValidator : AbstractValidator<SignalCabinetCommandRequest>
{
    public SignalCabinetCommandRequestValidator()
    {
        RuleFor(v => v.Target).IsInEnum().WithMessage("Geçersiz komut hedefi");

        RuleFor(v => v.TargetId)
            .NotNull().NotEqual(Guid.Empty)
            .When(v => v.Target != SignalCabinetOutput.Siren)
            .WithMessage("Komutun hedef kapısı seçilmeli");
    }
}
