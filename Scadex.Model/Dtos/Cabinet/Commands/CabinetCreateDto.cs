using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Cabinet.Commands;

public class CabinetCreateDto : IDto
{
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? GsmIp { get; set; }
    public string? NetworkIp { get; set; }
    public string? ScadaBaseUrl { get; set; }
    public int ScadaCommandTimeoutMs { get; set; }
    public bool ScadaIsEnabled { get; set; }
}

public class CabinetCreateDtoValidator : AbstractValidator<CabinetCreateDto>
{
    public CabinetCreateDtoValidator()
    {
        RuleFor(v => v.Name).NotNull().WithMessage("İsim bilgisi zorunlu lütfen kontrol ediniz");
        RuleFor(v => v.Name).MinimumLength(2).WithMessage("İsim bilgisi en az 2 karakter içermeli");
        RuleFor(v => v.CompanyId).NotNull().WithMessage("Firma bilgisi zorunlu lütfen kontrol ediniz");
        RuleFor(v => v.CompanyId).NotEqual(Guid.Empty).WithMessage("Firma bilgisi zorunlu lütfen kontrol ediniz");
        When(v => v.ScadaIsEnabled, () =>
        {
            RuleFor(v => v.ScadaBaseUrl).NotEmpty().WithMessage("SCADA acikken adres bilgisi zorunlu");
            RuleFor(v => v.ScadaCommandTimeoutMs).GreaterThanOrEqualTo(10000).WithMessage("Zaman aşımı en az 10.000ms olabilir");
        });
    }
}