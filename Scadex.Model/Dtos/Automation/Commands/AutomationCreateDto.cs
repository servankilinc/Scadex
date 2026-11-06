using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Automation.Commands;

public class AutomationCreateDto : IDto
{
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
}

public class AutomationCreateDtoValidator : AbstractValidator<AutomationCreateDto>
{
    public AutomationCreateDtoValidator()
    {
        RuleFor(v => v.Name).NotNull().WithMessage("İsim bilgisi zorunlu lütfen kontrol ediniz");
        RuleFor(v => v.Name).MinimumLength(2).WithMessage("İsim bilgisi en az 2 karakter içermeli");
        RuleFor(v => v.CompanyId).NotNull().WithMessage("Firma bilgisi zorunlu lütfen kontrol ediniz");
        RuleFor(v => v.CompanyId).NotEqual(Guid.Empty).WithMessage("Firma bilgisi zorunlu lütfen kontrol ediniz");
    }
}