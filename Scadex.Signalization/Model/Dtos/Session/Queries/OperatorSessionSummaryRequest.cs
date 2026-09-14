using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Session.Queries;

public class OperatorSessionSummaryRequest : IDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public Guid? CabinetId { get; set; }
}

public class OperatorSessionSummaryRequestValidator : AbstractValidator<OperatorSessionSummaryRequest>
{
    public OperatorSessionSummaryRequestValidator()
    {
        RuleFor(v => v.FromUtc).NotEmpty().WithMessage("Başlangıç tarihi zorunlu");
        RuleFor(v => v.ToUtc).NotEmpty().WithMessage("Bitiş tarihi zorunlu");
        RuleFor(v => v.ToUtc).GreaterThan(v => v.FromUtc).WithMessage("Bitiş tarihi başlangıçtan sonra olmalı");
        RuleFor(v => v).Must(v => (v.ToUtc - v.FromUtc).TotalDays <= 366).WithMessage("Özet en fazla 366 günlük aralık için alınabilir");
    }
}