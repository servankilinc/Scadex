using Scadex.Core.Utils.ResultPattern;
using Scadex.RemoteDesk.Model.Dtos.Pc.Queries;

namespace Scadex.RemoteDesk.Services.Abstract;

public interface IPcDirectoryService
{
    /// <summary> Aktif ve şablonu <c>DeviceType.Pc</c> olan cihazlar, kabin ve cihaz adına göre sıralı. </summary>
    Task<Result<ICollection<PcListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary> Tek PC + (bağlıysa) monitörleri ve her monitörün yayın durumu / izleyici sayısı. </summary>
    Task<Result<PcDetailDto>> GetAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
