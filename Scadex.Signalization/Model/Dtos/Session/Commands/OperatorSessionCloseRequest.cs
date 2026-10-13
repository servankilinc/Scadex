using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Session.Commands;

/// <summary> Açık operatör işlemini elle kapatma talebi (<c>Permission.SignalSessionClose</c>). </summary>
public class OperatorSessionCloseRequest : IDto
{
    /// <summary> Zorunlu gerekçe; işlemin zaman çizelgesinde kapatanla birlikte görünür (<c>OperatorSessionEvent.Detail</c>). </summary>
    public string Reason { get; set; } = null!;
}

/// <remarks> Frontend'deki zod şeması (<c>modules/signalization/models/session.ts</c>) bu kuralın elle tutulan kopyasıdır. </remarks>
public class OperatorSessionCloseRequestValidator : AbstractValidator<OperatorSessionCloseRequest>
{
    /// <summary> <c>OperatorSessionEvent.Detail</c> 512 karakterde kesilir; gerekçe kesilmeden sığsın. </summary>
    public const int ReasonMaxLength = 500;

    public OperatorSessionCloseRequestValidator()
    {
        RuleFor(v => v.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("Kapatma gerekçesi zorunlu")
            .MaximumLength(ReasonMaxLength).WithMessage($"Gerekçe en fazla {ReasonMaxLength} karakter olabilir");
    }
}
