using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Company.Commands;

public class CompanyCreateDto : IDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class CompanyCreateDtoValidator : AbstractValidator<CompanyCreateDto>
{
    public CompanyCreateDtoValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MinimumLength(2).WithMessage("Firma ismi en az 2 karakter olmalı");
    }
}