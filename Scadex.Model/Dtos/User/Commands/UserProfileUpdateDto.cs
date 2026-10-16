using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

/// <summary>
/// Kullanıcının kendi profilini güncellemesi (<c>PUT /api/Account/profile</c>). Kimlik gövdede değil token'dadır.
/// Kullanıcı adı, firma, kart ve aktiflik BİLEREK yok — onlar yönetici işlemidir.
/// </summary>
public class UserProfileUpdateDto : IDto
{
    public string FullName { get; set; } = null!;

    /// <summary> İsteğe bağlı; boş metin <c>null</c>'a çekilir. Doluysa sistem genelinde benzersiz olmalı (e-postayla giriş tekil eşleşmeye dayanır). </summary>
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <remarks> Kurallar <c>UserCreateDtoValidator</c> / <c>UserUpdateDtoValidator</c> ile aynıdır. </remarks>
public class UserProfileUpdateDtoValidator : AbstractValidator<UserProfileUpdateDto>
{
    public UserProfileUpdateDtoValidator()
    {
        RuleFor(v => v.FullName).NotEmpty().MinimumLength(4).WithMessage("Lütfen geçerli bir kullanıcı ism soyismi giriniz");
        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrWhiteSpace(v.Email)).WithMessage("Geçerli bir e-posta adresi giriniz");
        RuleFor(v => v.Email).MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir");
    }
}
