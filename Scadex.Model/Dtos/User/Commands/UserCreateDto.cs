using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

public class UserCreateDto : IDto
{
    public string UserName { get; set; } = null!;
    public string? Email { get; set; }
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    [CriticalData]
    public string Password { get; set; } = null!;
}

public class UserCreateDtoValidator : AbstractValidator<UserCreateDto>
{
    public UserCreateDtoValidator()
    {
        RuleFor(v => v.UserName).MinimumLength(4).WithMessage("Kullanıcı adı en az 4 karakter olmalı");
        RuleFor(v => v.CompanyId).NotEqual(Guid.Empty).WithMessage("Firma bilgisi zorunlu kontrol ediniz");
        RuleFor(v => v.FullName).MinimumLength(4).WithMessage("Lütfen geçerli bir kullanıcı ism soyismi giriniz");
        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrWhiteSpace(v.Email)).WithMessage("Geçerli bir e-posta adresi giriniz");
        RuleFor(v => v.Password).NotEmpty().MinimumLength(4).WithMessage("Parola en az 4 karakter olmalı");
    }
}