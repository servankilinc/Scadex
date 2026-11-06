using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;

namespace Scadex.Business.Abstract;

/// <summary>
/// Kameranın CİHAZ bilgisi (kamera ekranı: liste, detay, mevcut aktif kameranın bilgisini güncelleme). Kamera bir <c>Device</c>'tır ve
/// ek tablosu (<c>Camera</c>) çekirdektedir. İzleme, kare/klip çekimi ve MediaMTX yolları CameraManagement modülündedir
/// (<c>ICameraMediaService</c>); bilgi değişince modül <c>IDeviceChangeObserver</c> ile haberdar edilir.
/// </summary>
public interface ICameraService
{
    Task<Result<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CameraDto>>> GetListAsync(Guid automationId, bool includePassive = false, CancellationToken cancellationToken = default);

    /// <summary> Yalnızca AKTİF kameranın bilgisini günceller; commit sonrası <c>IDeviceChangeObserver</c>'ları çağırır. </summary>
    Task<Result> UpdateAsync(CameraUpdateDto request, CancellationToken cancellationToken = default);
}
