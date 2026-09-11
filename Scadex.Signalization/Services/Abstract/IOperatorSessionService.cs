using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Dtos.Session.Queries;

namespace Scadex.Signalization.Services.Abstract;

/// <summary> Operator islemlerinin okuma/rapor yuzu. Oturumlari yalnizca motor yazar. </summary>
public interface IOperatorSessionService
{
    /// <summary> Devam eden (dis kapisi kapanmamis) islemler — canli panel. Uyarili kapanmis oturumlar rapordan bulunur. </summary>
    Task<Result<ICollection<OperatorSessionOpenDto>>> GetOpenAsync(Guid? cabinetId, CancellationToken cancellationToken = default);

    Task<Result<PaginationResponse<OperatorSessionListItemDto>>> GetPagedAsync(OperatorSessionQueryRequest request, CancellationToken cancellationToken = default);

    Task<Result<OperatorSessionDetailDto>> GetDetailAsync(long sessionId, CancellationToken cancellationToken = default);

    Task<Result<OperatorSessionSummaryDto>> GetSummaryAsync(OperatorSessionSummaryRequest request, CancellationToken cancellationToken = default);
}
