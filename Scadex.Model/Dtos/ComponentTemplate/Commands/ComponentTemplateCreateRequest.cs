using Scadex.Core.Model;
using FluentValidation;
using EntityEnums = Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.ComponentTemplate.Commands;

public class ComponentTemplateCreateRequest : IDto
{
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string? BackgroundImageUrl { get; set; }

    public List<TemplatePinDraft> Pins { get; set; } = [];
}

public class TemplatePinDraft : IDto
{
    public string Name { get; set; } = null!;
    /// <summary>ComponentTemplate genisligine gore 0..1 normalize kesir (DB'de CHECK ile kisitli).</summary>
    public double RelativeX { get; set; }
    /// <summary>ComponentTemplate yuksekligine gore 0..1 normalize kesir (DB'de CHECK ile kisitli).</summary>
    public double RelativeY { get; set; }
    public EntityEnums.HandleSide Side { get; set; }
    public int? ChannelNumber { get; set; }
    public EntityEnums.PinFunction Function { get; set; }
    public EntityEnums.PinDirection Direction { get; set; }
    public EntityEnums.VoltageLevel? VoltageLevel { get; set; }
}

public class ComponentTemplateCreateRequestValidator : AbstractValidator<ComponentTemplateCreateRequest>
{
    public const int MaxPins = 256;

    public ComponentTemplateCreateRequestValidator()
    {
        RuleFor(v => v.Name).MinimumLength(2).WithMessage("En az 2 karakter icermeli");
        RuleFor(v => v.Width).GreaterThan(0).WithMessage("Genislik bilgisi bos gecilemez");
        RuleFor(v => v.Height).GreaterThan(0).WithMessage("Yukseklik bilgisi bos gecilemez");
        RuleFor(v => v.BackgroundColor).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Arka plan rengi #RRGGBB biciminde olmali");

        RuleFor(v => v.Pins.Count).LessThanOrEqualTo(MaxPins).OverridePropertyName("Pins").WithMessage($"Bir sablonda en fazla {MaxPins} pin olabilir");

        RuleForEach(v => v.Pins).SetValidator(new TemplatePinDraftValidator());

        // Burada yakalanmazsa DB kisit ihlali 500'e cevrilir
        RuleFor(v => v.Pins)
            .Must(pins => pins.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == pins.Count)
            .WithMessage("Ayni sablonda iki pin ayni ada sahip olamaz");
    }
}

public class TemplatePinDraftValidator : AbstractValidator<TemplatePinDraft>
{
    public TemplatePinDraftValidator()
    {
        RuleFor(v => v.Name).MinimumLength(1).WithMessage("Pin ismi en az 1 karakter icermeli");
        // DB'de CHECK (0..1) var; burada yakalamak 400 dondurur, yoksa kisit ihlali 500 olur.
        RuleFor(v => v.RelativeX).InclusiveBetween(0, 1).WithMessage("Konum x 0 ile 1 arasinda olmali");
        RuleFor(v => v.RelativeY).InclusiveBetween(0, 1).WithMessage("Konum y 0 ile 1 arasinda olmali");
        RuleFor(v => v.Side).IsInEnum().WithMessage("Kenar bilgisi gecersiz");
        RuleFor(v => v.Function).IsInEnum().WithMessage("Fonksiyon bilgisi gecersiz");
        RuleFor(v => v.Direction).IsInEnum().WithMessage("Yon bilgisi gecersiz");
    }
}
