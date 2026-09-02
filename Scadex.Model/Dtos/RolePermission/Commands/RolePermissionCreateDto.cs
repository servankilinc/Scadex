using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.RolePermission.Commands;

public class RolePermissionCreateDto : IDto
{
    public Guid RoleId { get; set; }
    public int PermissionId { get; set; }
}

public class RolePermissionCreateDtoValidator : AbstractValidator<RolePermissionCreateDto>
{
    public RolePermissionCreateDtoValidator()
    {
        RuleFor(v => v.RoleId).NotEmpty().WithMessage("Field cannot be empty");
        RuleFor(v => v.PermissionId).GreaterThanOrEqualTo(0).WithMessage("Field must be a valid permission id");
    }
}
