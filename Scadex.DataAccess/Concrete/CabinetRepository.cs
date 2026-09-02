using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CabinetRepository : RepositoryBase<Cabinet, AppDbContext>, ICabinetRepository
{
    public CabinetRepository(AppDbContext context) : base(context)
    {
    }
}