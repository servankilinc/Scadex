using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.RemoteDesk.Model.Dtos.Report.Queries;

namespace Scadex.RemoteDesk.Services.Abstract;

/// <summary>
/// PC izleme/kontrol denetim raporu (<c>RemotePcAudit</c>). Salt okunur: denetim satırlarını yalnızca izleme ve kontrol akışı yazar.
/// </summary>
public interface IRemoteAccessReportService
{
    /// <summary> Filtre seçenekleri: denetim tablolarında geçen PC'ler ve kullanıcılar (silinmiş/pasif olanlar dahil). </summary>
    Task<Result<RemoteAccessFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary> İzleme kayıtları (<c>ScreenViewLog</c>), yeniden eskiye. </summary>
    Task<Result<PaginationResponse<ScreenViewReportItemDto>>> GetViewsAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default);

    /// <summary> Uzaktan kontrol kayıtları (<c>RemoteControlSession</c>), yeniden eskiye. </summary>
    Task<Result<PaginationResponse<RemoteControlReportItemDto>>> GetControlsAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default);

    /// <summary> Aynı filtreyle özet: oturum sayıları, toplam süreler, en çok izlenen PC ve en çok izleyen kullanıcı. </summary>
    Task<Result<RemoteAccessSummaryDto>> GetSummaryAsync(RemoteAccessReportQuery query, CancellationToken cancellationToken = default);
}
