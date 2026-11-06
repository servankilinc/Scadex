using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Automation.Commands;

public class AutomationUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public bool IsActive { get; set; }
    /// <summary> <c>DeviceStatusId == Maintenance</c>. </summary>
    public bool IsUnderMaintenance { get; set; }
}

public class AutomationUpdateDtoValidator : AbstractValidator<AutomationUpdateDto>
{
    public AutomationUpdateDtoValidator()
    {
        RuleFor(v => v.Id).NotNull().WithMessage("Field cannot be null");
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Field mus be a valid guid value");
        RuleFor(v => v.Name).NotNull().WithMessage("İsim bilgisi zorunlu lütfen kontrol ediniz");
        RuleFor(v => v.Name).MinimumLength(2).WithMessage("İsim bilgisi en az 2 karakter içermeli");
    }
}