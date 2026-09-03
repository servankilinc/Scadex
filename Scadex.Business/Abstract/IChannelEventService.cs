using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Dtos.Scada.Commands;

namespace Scadex.Business.Abstract;

/// <summary> SCADA'dan gelen telemetrinin girdigi tek kapi. </summary>
public interface IChannelEventService
{
    /// <summary> Bir kabinin olay gecmisi — yeniden eskiye, sayfali. </summary>
    Task<Result<PaginationResponse<ChannelEventDto>>> GetPagedAsync(ChannelEventQueryRequest request, CancellationToken cancellationToken = default);


    /// <summary>
    /// Bir kabinin telemetri paketini isler.
    /// Tanınmayan cihaz kodu / kanal numarasi için tum istek reddedilmez devam edilir <c>Warning</c> seviyesinde log kaydı atılır. 
    /// </summary>
    Task<Result> IngestAsync(ScadaIngestRequest request, CancellationToken cancellationToken = default);

    /// <summary> Haber alinamayan cihazlari <c>Offline</c>'a ceker ve degisenleri yayinlar. </summary>
    /// <returns> Offline'a cekilen cihaz sayisi. </returns>
    Task<int> SweepStaleDevicesAsync(TimeSpan staleAfter, CancellationToken cancellationToken = default);
}
