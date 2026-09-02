using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Role.Commands;

public class RoleCreateDto : IDto
{
    public string Name { get; set; } = null!;
}

public class RoleCreateDtoValidator : AbstractValidator<RoleCreateDto>
{
    public RoleCreateDtoValidator()
    {
        RuleFor(v => v.Name).MinimumLength(4).WithMessage("İsim bilgisi en az 4 karakter içermeli");
    }
}