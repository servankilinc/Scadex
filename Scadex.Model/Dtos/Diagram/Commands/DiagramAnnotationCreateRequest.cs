using FluentValidation;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary> Paletten not ekleme: <c>POST /api/Diagram/cabinet/{cabinetId}/annotations</c>. Id'yi istemci üretir. </summary>
public class DiagramAnnotationCreateRequest : DiagramAnnotationUpdateRequest
{
    public Guid Id { get; set; }
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class DiagramAnnotationCreateRequestValidator : AbstractValidator<DiagramAnnotationCreateRequest>
{
    public DiagramAnnotationCreateRequestValidator()
    {
        Include(new DiagramAnnotationUpdateRequestValidator());

        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Not kimligi zorunlu");
        RuleFor(v => v.Width).GreaterThan(0).WithMessage("Genislik sifirdan buyuk olmali");
        RuleFor(v => v.Height).GreaterThan(0).WithMessage("Yukseklik sifirdan buyuk olmali");
    }
}
