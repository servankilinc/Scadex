using FluentValidation;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Commands.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Commands.Items;

public class DiagramAnnotationDraft : IDto, IIdentifiableDraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
    public string Text { get; set; } = null!;
    public AnnotationShape Shape { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string FontColor { get; set; } = null!;
    public double FontSize { get; set; }
    public bool IsBold { get; set; }
    public string BorderColor { get; set; } = null!;
}

public class DiagramAnnotationDraftValidator : AbstractValidator<DiagramAnnotationDraft>
{
    public DiagramAnnotationDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Not kimligi zorunlu");
        RuleFor(v => v.Name).NotEmpty().WithMessage("Not adi zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("Not adi en fazla 128 karakter olabilir");
        RuleFor(v => v.Text).MaximumLength(4000).WithMessage("Not metni en fazla 4000 karakter olabilir");
        RuleFor(v => v.Shape).IsInEnum().WithMessage("Gecersiz not sekli");
        RuleFor(v => v.Width).GreaterThan(0).WithMessage("Genislik sifirdan buyuk olmali");
        RuleFor(v => v.Height).GreaterThan(0).WithMessage("Yukseklik sifirdan buyuk olmali");
        RuleFor(v => v.FontSize).InclusiveBetween(1, 200).WithMessage("Yazi boyutu 1 ile 200 arasinda olmali");
        RuleFor(v => v.BackgroundColor).NotEmpty().WithMessage("Arka plan rengi zorunlu");
        RuleFor(v => v.BackgroundColor).MaximumLength(32).WithMessage("Arka plan rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.FontColor).NotEmpty().WithMessage("Yazi rengi zorunlu");
        RuleFor(v => v.FontColor).MaximumLength(32).WithMessage("Yazi rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.BorderColor).NotEmpty().WithMessage("Kenarlik rengi zorunlu");
        RuleFor(v => v.BorderColor).MaximumLength(32).WithMessage("Kenarlik rengi en fazla 32 karakter olabilir");
    }
}
