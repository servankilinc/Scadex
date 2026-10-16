using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using FluentValidation;

namespace Scadex.Model.Dtos.User.Commands;

/// <summary> Kullanıcının kendi parolasını değiştirmesi (<c>PUT /api/Account/password</c>).  </summary>
public class UserOwnPasswordChangeDto : IDto
{
    [CriticalData]
    public string CurrentPassword { get; set; } = null!;

    [CriticalData]
    public string NewPassword { get; set; } = null!;

    /// <summary>
    /// İsteği gönderen cihaz. Bu cihazın oturumu korunur, kullanıcının DİĞER cihazlardaki oturumları yeniden giriş ister.
    /// <c>null</c> ise bütün oturumlar düşer.
    /// </summary>
    public Guid? DeviceId { get; set; }
}

/// <remarks> Yeni parola kuralı Identity'nin parola ayarıyla (Program.cs) ve <c>UserPasswordChangeDtoValidator</c> ile aynıdır. </remarks>
public class UserOwnPasswordChangeDtoValidator : AbstractValidator<UserOwnPasswordChangeDto>
{
    public UserOwnPasswordChangeDtoValidator()
    {
        RuleFor(v => v.CurrentPassword).NotEmpty().WithMessage("Mevcut parola boş olamaz");
        RuleFor(v => v.NewPassword).NotEmpty().MinimumLength(4).WithMessage("Parola en az 4 karakter olmalı");
        RuleFor(v => v.NewPassword).MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir");
        RuleFor(v => v.NewPassword).NotEqual(v => v.CurrentPassword).WithMessage("Yeni parola mevcut paroladan farklı olmalı");
    }
}
