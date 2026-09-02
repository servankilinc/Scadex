using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

public class UserUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
}

public class UserUpdateDtoValidator : AbstractValidator<UserUpdateDto>
{
    public UserUpdateDtoValidator()
    {
        RuleFor(v => v.Id).NotNull().WithMessage("Field cannot be null");
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Field mus be a valid guid value");
        RuleFor(v => v.FullName).MinimumLength(4).WithMessage("Lütfen geçerli bir kullanıcı ism soyismi giriniz");
    }
}