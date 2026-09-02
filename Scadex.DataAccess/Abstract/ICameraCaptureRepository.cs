using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface ICameraCaptureRepository : IRepository<CameraCapture>, IRepositoryAsync<CameraCapture>
{
    /// <summary> Bir kameranin son cekimleri, yeniden eskiye. </summary> 
    Task<ICollection<CameraCapture>> GetRecentForCameraAsync(Guid cameraId, int take, CancellationToken cancellationToken = default);
}
