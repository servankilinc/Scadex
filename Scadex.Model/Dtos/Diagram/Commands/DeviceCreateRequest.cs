using FluentValidation;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Paletten bırakma ve kopyalama: <c>POST /api/Diagram/cabinet/{cabinetId}/devices</c>. Cihazın Id'sini istemci üretir
/// (<c>newId()</c>); pinleri ve kanalları sunucu şablondan üretir ve yanıtta döndürür — istemci pin kimliği göndermez.
/// </summary>
public class DeviceCreateRequest : DeviceUpdateRequest
{
    public Guid Id { get; set; }
    public Guid ComponentTemplateId { get; set; }

    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }

    /// <summary> <c>null</c> = şablon boyutunu takip et. </summary>
    public double? Width { get; set; }
    public double? Height { get; set; }

    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class DeviceCreateRequestValidator : AbstractValidator<DeviceCreateRequest>
{
    public DeviceCreateRequestValidator()
    {
        Include(new DeviceUpdateRequestValidator());

        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Cihaz kimligi zorunlu");
        RuleFor(v => v.ComponentTemplateId).NotEqual(Guid.Empty).WithMessage("Sablon secilmeli");
        RuleFor(v => v.Width).GreaterThan(0).When(v => v.Width.HasValue).WithMessage("Genislik sifirdan buyuk olmali");
        RuleFor(v => v.Height).GreaterThan(0).When(v => v.Height.HasValue).WithMessage("Yukseklik sifirdan buyuk olmali");
    }
}
