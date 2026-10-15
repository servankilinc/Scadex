using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

/// <summary>
/// Yetkili kullanıcının (<c>ManageUsers</c>) başka bir kullanıcının parolasını değiştirmesi. Eski parola sorulmaz; değişince
/// kullanıcının refresh token'ları iptal edilir ve kilit (lockout) sıfırlanır.
/// </summary>
public class UserPasswordChangeDto : IDto
{
    public string NewPassword { get; set; } = null!;
}

/// <remarks> Kural Identity'nin parola ayarıyla (Program.cs) ve <c>UserCreateDtoValidator</c> ile aynıdır. </remarks>
public class UserPasswordChangeDtoValidator : AbstractValidator<UserPasswordChangeDto>
{
    public UserPasswordChangeDtoValidator()
    {
        RuleFor(v => v.NewPassword).NotEmpty().MinimumLength(4).WithMessage("Parola en az 4 karakter olmalı");
        RuleFor(v => v.NewPassword).MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir");
    }
}
