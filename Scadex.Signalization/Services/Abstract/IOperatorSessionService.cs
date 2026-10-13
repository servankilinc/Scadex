using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Model.Dtos.Session.Commands;
using Scadex.Signalization.Model.Dtos.Session.Queries;

namespace Scadex.Signalization.Services.Abstract;

/// <summary> Operator islemlerinin okuma/rapor yuzu ve elle kapatma talebi. Oturumlari yalnizca motor yazar. </summary>
public interface IOperatorSessionService
{
    /// <summary>
    /// Açık işlemi elle kapatır (<c>Permission.SignalSessionClose</c>). Talep kabin şeridinde motorca işlenir: siren talebi
    /// bırakılır, iç kapılara otomatik kilitleme denenir, durum <c>ManuallyClosed</c> olur. Motor bitirene kadar bekler.
    /// </summary>
    Task<Result> CloseAsync(long sessionId, Guid userId, OperatorSessionCloseRequest request, CancellationToken cancellationToken = default);

    /// <summary> Devam eden (dis kapisi kapanmamis) islemler — canli panel. Uyarili kapanmis oturumlar rapordan bulunur. </summary>
    Task<Result<ICollection<OperatorSessionOpenDto>>> GetOpenAsync(Guid? cabinetId, CancellationToken cancellationToken = default);

    Task<Result<PaginationResponse<OperatorSessionListItemDto>>> GetPagedAsync(OperatorSessionQueryRequest request, CancellationToken cancellationToken = default);

    Task<Result<OperatorSessionDetailDto>> GetDetailAsync(long sessionId, CancellationToken cancellationToken = default);

    Task<Result<OperatorSessionSummaryDto>> GetSummaryAsync(OperatorSessionSummaryRequest request, CancellationToken cancellationToken = default);

    /// <summary> Kabinin kart okuyucusundaki son okumalar (en yenisi basta). </summary>
    Task<Result<ICollection<SignalCardReadDto>>> GetRecentCardReadsAsync(Guid cabinetId, CancellationToken cancellationToken = default);
}
