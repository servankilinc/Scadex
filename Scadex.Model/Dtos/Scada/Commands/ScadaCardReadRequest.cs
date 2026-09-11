using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> SCADA'nin kabindeki bir kart okuyucudan okudugu kart kimligi. </summary>
public class ScadaCardReadRequest : IDto
{
    /// <summary> Scada MAC adresi </summary>
    public string MacAddress { get; set; } = null!;
    public string CardId { get; set; } = null!;
    public DateTime? TimestampUtc { get; set; }
}

public class ScadaCardReadRequestValidator : AbstractValidator<ScadaCardReadRequest>
{
    public ScadaCardReadRequestValidator()
    {
        RuleFor(v => v.MacAddress).NotEmpty().WithMessage("macAddress zorunlu");
        RuleFor(v => v.MacAddress).MaximumLength(64).WithMessage("macAddress en fazla 64 karakter olabilir");
        RuleFor(v => v.CardId).NotEmpty().WithMessage("cardId zorunlu");
        RuleFor(v => v.CardId).MaximumLength(64).WithMessage("cardId en fazla 64 karakter olabilir");
    }
}
