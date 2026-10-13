using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

/// <summary>  Sahada okutulan kartı bir kullanıcıya tanıtmak içindir.  </summary>
public class UserIdentityCardAssignDto : IDto
{
    public string IdentityCardId { get; set; } = null!;

    /// <summary> Kullanıcının zaten FARKLI bir kartı varsa üzerine yazılsın mı? <c>false</c> iken mevcut kart korunur ve 400 döner. </summary>
    public bool ReplaceExisting { get; set; }
}

public class UserIdentityCardAssignDtoValidator : AbstractValidator<UserIdentityCardAssignDto>
{
    public UserIdentityCardAssignDtoValidator()
    {
        RuleFor(v => v.IdentityCardId).NotEmpty().WithMessage("Kart numarası boş olamaz");
        RuleFor(v => v.IdentityCardId).MaximumLength(64).WithMessage("Kart numarası en fazla 64 karakter olabilir");
    }
}
