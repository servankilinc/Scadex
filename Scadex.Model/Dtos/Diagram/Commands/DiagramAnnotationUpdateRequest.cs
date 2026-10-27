using FluentValidation;
using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Notun içeriği: <c>PUT /api/Diagram/cabinet/{cabinetId}/annotations/{id}</c>. Konum, boyut, z-sırası, kilit ve görünürlük
/// <see cref="DiagramLayoutRequest"/>'tedir.
/// </summary>
public class DiagramAnnotationUpdateRequest : IDto
{
    public string Name { get; set; } = null!;
    public string Text { get; set; } = null!;
    public AnnotationShape Shape { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string FontColor { get; set; } = null!;
    public double FontSize { get; set; }
    public bool IsBold { get; set; }
    public string BorderColor { get; set; } = null!;
}

public class DiagramAnnotationUpdateRequestValidator : AbstractValidator<DiagramAnnotationUpdateRequest>
{
    public DiagramAnnotationUpdateRequestValidator()
    {
        RuleFor(v => v.Name).NotEmpty().WithMessage("Not adi zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("Not adi en fazla 128 karakter olabilir");
        RuleFor(v => v.Text).MaximumLength(4000).WithMessage("Not metni en fazla 4000 karakter olabilir");
        RuleFor(v => v.Shape).IsInEnum().WithMessage("Gecersiz not sekli");
        RuleFor(v => v.FontSize).InclusiveBetween(1, 200).WithMessage("Yazi boyutu 1 ile 200 arasinda olmali");
        RuleFor(v => v.BackgroundColor).NotEmpty().WithMessage("Arka plan rengi zorunlu");
        RuleFor(v => v.BackgroundColor).MaximumLength(32).WithMessage("Arka plan rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.FontColor).NotEmpty().WithMessage("Yazi rengi zorunlu");
        RuleFor(v => v.FontColor).MaximumLength(32).WithMessage("Yazi rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.BorderColor).NotEmpty().WithMessage("Kenarlik rengi zorunlu");
        RuleFor(v => v.BorderColor).MaximumLength(32).WithMessage("Kenarlik rengi en fazla 32 karakter olabilir");
    }
}
