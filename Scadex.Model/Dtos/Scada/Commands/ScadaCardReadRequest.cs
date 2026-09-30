using FluentValidation;
using Scadex.Core.Model;
using Scadex.Core.Utils;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> SCADA'nin kabindeki bir kart okuyucudan okudugu kart kimligi. </summary>
public class ScadaCardReadRequest : IDto
{
    private string _macAddress = null!;

    /// <summary> Scada MAC adresi — gelirken tek tipe çevrilir (<c>AA:BB:CC:DD:EE:FF</c>, <see cref="MacAddressFormat"/>). </summary>
    public string MacAddress { get => _macAddress; set => _macAddress = MacAddressFormat.NormalizeOrKeep(value)!; }
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
