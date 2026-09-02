using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Role.Commands;

public class RoleUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class RoleUpdateDtoValidator : AbstractValidator<RoleUpdateDto>
{
    public RoleUpdateDtoValidator()
    {
        RuleFor(v => v.Id).NotNull().WithMessage("Field cannot be null");
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Field mus be a valid guid value");
        RuleFor(v => v.Name).MinimumLength(4).WithMessage("İsim bilgisi en az 4 karakter içermeli");
    }
}