using FluentValidation;

namespace Scadex.Model.Auth.Refresh;

public class RefreshAuthRequest
{
    public string? RefreshToken { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
}

public class RefreshAuthRequestValidator : AbstractValidator<RefreshAuthRequest>
{
    public RefreshAuthRequestValidator()
    {
        RuleFor(b => b.UserId).NotNull().NotEmpty();
        RuleFor(b => b.DeviceId).NotNull().NotEqual(Guid.Empty).NotEmpty();
    }
}