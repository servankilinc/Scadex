using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CanvasSettingsRepository : RepositoryBase<CanvasSettings, AppDbContext>, ICanvasSettingsRepository
{
    public CanvasSettingsRepository(AppDbContext context) : base(context)
    {
    }
}