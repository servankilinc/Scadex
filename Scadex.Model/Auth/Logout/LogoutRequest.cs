using FluentValidation;

namespace Scadex.Model.Auth.Logout;

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(b => b.UserId).NotEmpty();
        RuleFor(b => b.DeviceId).NotEqual(Guid.Empty).NotEmpty();
    }
}