using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Tuvaldeki yerleşim: <c>PUT /api/Diagram/cabinet/{cabinetId}/layout</c>. Sürükleme bitişi, hizala/dağıt, ok tuşuyla itme,
/// boyut, z-sırası, kilit ve görünürlük buradan yazılır. Diyagramın kalan TEK toplu ucudur ve bilerek referans taşımaz:
/// her kayıt yalnızca kendi satırını günceller, sıralama ya da kayıtlar arası tutarlılık sorusu doğmaz.
/// </summary>
public class DiagramLayoutRequest : IDto
{
    public List<DeviceLayoutItem> Devices { get; set; } = [];
    public List<AnnotationLayoutItem> Annotations { get; set; } = [];
}

public class DeviceLayoutItem : IDto
{
    public Guid Id { get; set; }
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }

    /// <summary> <c>null</c> "dokunma" DEGIL, "sablon boyutuna don" demektir. </summary>
    public double? Width { get; set; }
    public double? Height { get; set; }

    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class AnnotationLayoutItem : IDto
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

public class DiagramLayoutRequestValidator : AbstractValidator<DiagramLayoutRequest>
{
    public DiagramLayoutRequestValidator()
    {
        RuleFor(v => v.Devices).Must(items => items.Select(i => i.Id).Distinct().Count() == items.Count).WithMessage("Ayni cihaz birden fazla kez var");
        RuleFor(v => v.Annotations).Must(items => items.Select(i => i.Id).Distinct().Count() == items.Count).WithMessage("Ayni not birden fazla kez var");

        RuleForEach(v => v.Devices).ChildRules(item =>
        {
            item.RuleFor(i => i.Width).GreaterThan(0).When(i => i.Width.HasValue).WithMessage("Genislik sifirdan buyuk olmali");
            item.RuleFor(i => i.Height).GreaterThan(0).When(i => i.Height.HasValue).WithMessage("Yukseklik sifirdan buyuk olmali");
        });
        RuleForEach(v => v.Annotations).ChildRules(item =>
        {
            item.RuleFor(i => i.Width).GreaterThan(0).WithMessage("Genislik sifirdan buyuk olmali");
            item.RuleFor(i => i.Height).GreaterThan(0).WithMessage("Yukseklik sifirdan buyuk olmali");
        });
    }
}
