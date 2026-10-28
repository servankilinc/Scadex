using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Dtos.Scada.Queries;

namespace Scadex.Business.Abstract;

/// <summary> SCADA'dan gelen telemetrinin girdigi tek kapi: kartın bildirdiği (ingest) ve karttan okunan (<c>/status</c>) değerler. </summary>
public interface IChannelEventService
{
    /// <summary> Bir kabinin olay gecmisi — yeniden eskiye, sayfali. </summary>
    Task<Result<PaginationResponse<ChannelEventDto>>> GetPagedAsync(ChannelEventQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir kabinin telemetri paketini isler.
    /// Tanınmayan cihaz kodu / kanal numarasi için tum istek reddedilmez devam edilir <c>Warning</c> seviyesinde log kaydı atılır.
    /// <paramref name="cabinetId"/> isteği gönderen kontrol kartının MAC kimliğinden (<c>DeviceId</c> header'ı) gelir (controller), gövdeden değil.
    /// </summary>
    Task<Result> IngestAsync(Guid cabinetId, ScadaIngestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kabinin kartını okur (<c>GET {ScadaBaseUrl}/status</c>) ve değeri değişen kanalları ingest'le aynı yoldan yazar
    /// (kaynak <c>Sync</c>: sinyalizasyon motoru bunları olay olarak işlemez). Kartın bildirmediği kanala dokunulmaz.
    /// Kartın kendisinden gelen başarısızlık (hata yanıtı / yanıt yok) <c>Result</c> hatası DEĞİLDİR, <c>ScadaSyncResultDto.Status</c>'te döner;
    /// yanıt yoksa kontrol modülü Offline'a çekilir. <c>Result</c> hatası yalnızca kabin/SCADA yapılandırması içindir. Yeniden deneme yoktur.
    /// </summary>
    /// <param name="timeout">
    /// Verilmezse kabinin komut zaman aşımı (<c>IScadaCommandGateway.RequestTimeout</c>, 5 sn – 3 dk). İzleme turu kısa bir süre verir:
    /// worker kaynakları sırayla yoklar, yanıtsız bir kart tüm turu kabinin komut süresi kadar bekletmesin.
    /// </param>
    Task<Result<ScadaSyncResultDto>> SyncFromScadaAsync(Guid cabinetId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
