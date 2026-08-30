using FluentValidation;
using Scadex.Core.Utils.CriticalData;

namespace Scadex.Model.Auth.Login;

public class LoginRequest
{
    public string? Email { get; set; }
    public string? UserName { get; set; }

    [CriticalData]
    public string Password { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public string ClientType { get; set; } = null!;
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(b => b).Must(b => !string.IsNullOrWhiteSpace(b.Email) || !string.IsNullOrWhiteSpace(b.UserName)).WithMessage("Either Email or UserName must be provided.");
        RuleFor(b => b.Email).NotEmpty().EmailAddress().When(b => string.IsNullOrWhiteSpace(b.UserName));
        RuleFor(b => b.UserName).NotEmpty().When(b => string.IsNullOrWhiteSpace(b.Email));
        RuleFor(b => b.Password).NotNull().NotEmpty();
        RuleFor(b => b.ClientType).NotNull().NotEmpty();
    }
}