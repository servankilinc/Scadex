using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Company.Commands;

public class CompanyUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class CompanyUpdateDtoValidator : AbstractValidator<CompanyUpdateDto>
{
    public CompanyUpdateDtoValidator()
    {
        RuleFor(v => v.Id).NotNull().WithMessage("Field cannot be null");
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Field mus be a valid guid value");
        RuleFor(v => v.Name).NotEmpty().MinimumLength(2).WithMessage("Firma ismi en az 2 karkter olmalı");
    }
}