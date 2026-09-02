using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CameraCaptureRepository : RepositoryBase<CameraCapture, AppDbContext>, ICameraCaptureRepository
{
    public CameraCaptureRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ICollection<CameraCapture>> GetRecentForCameraAsync(Guid cameraId, int take, CancellationToken cancellationToken = default)
    {
        // Siralama CapturedAtUtc'ye gore — satirin YAZILDIGI an degil,
        // goruntunun ANI. Ikisi ayrisabilir: bir klip olay oncesini de
        // kapsadigi icin cekim isteginden ONCEKI bir ani tasiyabilir.
        // Esitlikte Id kirilir ki sira kararli olsun.
        return await _context.CameraCaptures
            .AsNoTracking()
            .Where(p => p.CameraId == cameraId)
            .OrderByDescending(p => p.CapturedAtUtc)
            .ThenByDescending(p => p.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
