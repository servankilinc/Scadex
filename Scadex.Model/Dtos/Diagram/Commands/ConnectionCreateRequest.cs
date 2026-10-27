using FluentValidation;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Kablo çizme: <c>POST /api/Diagram/cabinet/{cabinetId}/connections</c>. Id'yi istemci üretir; iki uç da bu kabinde
/// KALICI pinler olmalıdır (cihaz bırakılınca anında oluşturulduğu için kaydedilmemiş pin yoktur).
/// </summary>
public class ConnectionCreateRequest : ConnectionUpdateRequest
{
    public Guid Id { get; set; }
    public Guid SourcePinId { get; set; }
    public Guid TargetPinId { get; set; }
}

public class ConnectionCreateRequestValidator : AbstractValidator<ConnectionCreateRequest>
{
    public ConnectionCreateRequestValidator()
    {
        Include(new ConnectionUpdateRequestValidator());

        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Kablo kimligi zorunlu");
        RuleFor(v => v.SourcePinId).NotEqual(Guid.Empty).WithMessage("Kaynak pin kimligi zorunlu");
        RuleFor(v => v.TargetPinId).NotEqual(Guid.Empty).WithMessage("Hedef pin kimligi zorunlu");
        RuleFor(v => v.TargetPinId).NotEqual(v => v.SourcePinId).WithMessage("Bir pin kendisine baglanamaz");
    }
}
