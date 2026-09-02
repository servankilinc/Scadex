using Scadex.Core.Model;
using Scadex.Model.Enums;
using FluentValidation;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> SCADA'nın HTTP uzerinden BIZE push ettiği telemetri bilgisi. </summary>
public class ScadaIngestRequest : IDto
{
    public Guid CabinetId { get; set; }

    /// <summary> Ölçümün SCADA tarafindaki zamani. Kritik bir bilgi değil bilgi amaclidir cunku SCADA'nin saati kaymis olabilir veya (opsiyonel old için göndermiyor olabailir scada). </summary>
    public DateTime? TimestampUtc { get; set; }
    public List<ScadaDeviceReading> Devices { get; set; } = [];
}

public class ScadaDeviceReading
{
    public string ExternalCode { get; set; } = null!;
    public EntityEnums.DeviceStatus? StatusId { get; set; }
    public List<ScadaChannelReading> Channels { get; set; } = [];
}

public class ScadaChannelReading
{
    public int ChannelNumber { get; set; }

    /// <summary> Burası döküman okunup iş kuralları ile işlenecek. Analog sinayaller vs. için farklı formatlarda gelebilir hex kodu gibi. </summary>
    public string? Value { get; set; }
}

public class ScadaIngestRequestValidator : AbstractValidator<ScadaIngestRequest>
{
    public ScadaIngestRequestValidator()
    {
        RuleFor(v => v.CabinetId).NotEmpty().WithMessage("cabinetId zorunlu");

        RuleFor(v => v.Devices).NotNull().WithMessage("devices zorunlu");
        RuleForEach(v => v.Devices).ChildRules(device =>
        {
            device.RuleFor(d => d.ExternalCode).NotEmpty().WithMessage("externalCode zorunlu");
            device.RuleFor(d => d.ExternalCode).MaximumLength(64).WithMessage("externalCode en fazla 64 karakter olabilir");

            // Null gecerli ("dokunma"); dolu ise tanimli bir deger olmali.
            device.RuleFor(d => d.StatusId).IsInEnum().When(d => d.StatusId.HasValue).WithMessage("Gecersiz cihaz durumu");

            device.RuleFor(d => d.Channels).NotNull().WithMessage("channels zorunlu");
            device.RuleForEach(d => d.Channels).ChildRules(channel =>
            {
                channel.RuleFor(c => c.ChannelNumber).GreaterThan(0).WithMessage("channelNumber sifirdan buyuk olmali");

                // Deger NULL olabilir: "kanal var ama okunamadi" mesru bir durum.
                channel.RuleFor(c => c.Value).MaximumLength(256).WithMessage("value en fazla 256 karakter olabilir");
            });
        });
    }
}
