using Scadex.Core.Utils.ResultPattern;
using Scadex.RemoteDesk.Model.Dtos.View.Queries;

namespace Scadex.RemoteDesk.Services.Abstract;

/// <summary> Tarayıcının izleme akışı: başlat → (15 sn'de bir) yenile → bırak (RemoteDesk.md § 8). </summary>
public interface IScreenViewService
{
    /// <summary> Yayın yoksa PC'ye komut gider ve yol hazır olana kadar (en fazla ~15 sn) beklenir. </summary>
    Task<Result<ScreenViewDto>> StartAsync(Guid deviceId, int monitorIndex, CancellationToken cancellationToken = default);

    /// <summary> Düşmüş ya da başka kullanıcının kiralaması 404'tür. </summary>
    Task<Result> RenewAsync(Guid viewId, CancellationToken cancellationToken = default);

    /// <summary> Bilinmeyen kiralamada da başarılıdır. </summary>
    Task<Result> ReleaseAsync(Guid viewId, CancellationToken cancellationToken = default);
}
