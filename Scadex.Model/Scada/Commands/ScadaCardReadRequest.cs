using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Scada.Commands;

/// <summary>
/// SCADA'nin otomasyondaki bir kart okuyucudan okudugu kart kimligi. Otomasyon, isteği gönderen kontrol kartının Basic Auth kimliğinden çözülür (gövdede yoktur).
/// </summary>
public class ScadaCardReadRequest : IDto
{
    public string CardId { get; set; } = null!;
    public DateTime? TimestampUtc { get; set; }
}

public class ScadaCardReadRequestValidator : AbstractValidator<ScadaCardReadRequest>
{
    public ScadaCardReadRequestValidator()
    {
        RuleFor(v => v.CardId).NotEmpty().WithMessage("cardId zorunlu");
        RuleFor(v => v.CardId).MaximumLength(64).WithMessage("cardId en fazla 64 karakter olabilir");
    }
}
