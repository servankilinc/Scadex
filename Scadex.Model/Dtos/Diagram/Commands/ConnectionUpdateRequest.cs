using FluentValidation;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Common;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Kablonun görünümü: <c>PUT /api/Diagram/cabinet/{cabinetId}/connections/{id}</c>. Uçlar burada yoktur — ucu değiştirmek
/// yeni bir kablodur (silip yeniden çizilir).
/// </summary>
public class ConnectionUpdateRequest : IDto
{
    public string? Label { get; set; }
    public WireType WireType { get; set; }
    public string Color { get; set; } = null!;
    public LineStyle LineStyle { get; set; }
    public double StrokeWidth { get; set; }
    public EdgeRouting Routing { get; set; }

    /// <summary>Ara kirilma noktalari, kaynak -> hedef sirali, IKI UC NOKTA HARIC. Bos olabilir.</summary>
    public List<PointDto> Waypoints { get; set; } = [];
    public int ZIndex { get; set; }
}

public class ConnectionUpdateRequestValidator : AbstractValidator<ConnectionUpdateRequest>
{
    public ConnectionUpdateRequestValidator()
    {
        RuleFor(v => v.Label).MaximumLength(128).WithMessage("Etiket en fazla 128 karakter olabilir");
        RuleFor(v => v.WireType).IsInEnum().WithMessage("Gecersiz kablo tipi");
        RuleFor(v => v.Color).NotEmpty().WithMessage("Kablo rengi zorunlu");
        RuleFor(v => v.Color).MaximumLength(32).WithMessage("Kablo rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.LineStyle).IsInEnum().WithMessage("Gecersiz cizgi stili");
        RuleFor(v => v.Routing).IsInEnum().WithMessage("Gecersiz cizim sekli");
        RuleFor(v => v.StrokeWidth).GreaterThan(0).WithMessage("Kalinlik sifirdan buyuk olmali");
        RuleFor(v => v.Waypoints).Must(w => w.Count <= 64).WithMessage("Bir kabloda en fazla 64 kirilma noktasi olabilir");
    }
}
