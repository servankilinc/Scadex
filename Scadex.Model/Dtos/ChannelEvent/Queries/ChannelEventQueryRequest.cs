using FluentValidation;
using Scadex.Core.Model;
using Scadex.Core.Utils.Pagination;

namespace Scadex.Model.Dtos.ChannelEvent.Queries;

public class ChannelEventQueryRequest : IDto
{
    public Guid AutomationId { get; set; }
    public Guid? IoChannelId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Sayfalama altyapisinin bekledigi sekle cevirir.</summary>
    public PaginationRequest ToPaginationRequest() => new() { Page = Page, PageSize = PageSize };
}

public class ChannelEventQueryRequestValidator : AbstractValidator<ChannelEventQueryRequest>
{
    public ChannelEventQueryRequestValidator()
    {
        RuleFor(v => v.AutomationId).NotEqual(Guid.Empty).WithMessage("Otomasyon bilgisi zorunlu");
        RuleFor(v => v.ToUtc).GreaterThanOrEqualTo(v => v.FromUtc!.Value)
            .When(v => v.FromUtc.HasValue && v.ToUtc.HasValue)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz");
    }
}
