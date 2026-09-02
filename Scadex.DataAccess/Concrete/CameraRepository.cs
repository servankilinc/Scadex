using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CameraRepository : RepositoryBase<Camera, AppDbContext>, ICameraRepository
{
    public CameraRepository(AppDbContext context) : base(context)
    {
    }
}
