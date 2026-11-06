using FluentValidation;
using Scadex.Core.Model;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Model.Dtos.Session.Queries;

public class OperatorSessionQueryRequest : IDto
{
    public Guid? CabinetId { get; set; }
    public Guid? OuterDoorId { get; set; }
    public Guid? UserId { get; set; }

    public Guid? AuthorityId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public OperatorSessionStatus? Status { get; set; }

    /// <summary> Verilen bayraklardan EN AZ BIRINI tasiyan oturumlar. </summary>
    public SessionFlags? Flags { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class OperatorSessionQueryRequestValidator : AbstractValidator<OperatorSessionQueryRequest>
{
    public OperatorSessionQueryRequestValidator()
    {
        RuleFor(v => v.Page).GreaterThanOrEqualTo(1).WithMessage("Sayfa 1'den küçük olamaz");
        RuleFor(v => v.PageSize).InclusiveBetween(1, 200).WithMessage("Sayfa boyutu 1-200 arasında olmalı");
        RuleFor(v => v.ToUtc).GreaterThanOrEqualTo(v => v.FromUtc).When(v => v.FromUtc != null && v.ToUtc != null).WithMessage("Bitiş tarihi başlangıçtan önce olamaz");
    }
}
