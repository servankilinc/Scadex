using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CameraCaptureSettingRepository : RepositoryBase<CameraCaptureSetting, AppDbContext>, ICameraCaptureSettingRepository
{
    public CameraCaptureSettingRepository(AppDbContext context) : base(context)
    {
    }
}
