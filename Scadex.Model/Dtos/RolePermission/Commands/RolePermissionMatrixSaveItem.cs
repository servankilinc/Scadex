using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.RolePermission.Commands;

public class RolePermissionMatrixSaveItem : IDto
{
    public Guid RoleId { get; set; }
    public ICollection<int> PermissionIds { get; set; } = new List<int>();
}

public class RolePermissionMatrixSaveItemValidator : AbstractValidator<RolePermissionMatrixSaveItem>
{
    public RolePermissionMatrixSaveItemValidator()
    {
        RuleFor(v => v.RoleId).NotEqual(Guid.Empty).WithMessage("Rol bilgisi zorunlu");
        RuleFor(v => v.PermissionIds).NotNull().WithMessage("İzin listesi boş olamaz (izinsiz rol için boş dizi gönderin)");
    }
}
