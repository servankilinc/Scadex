using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class PinRepository : RepositoryBase<Pin, AppDbContext>, IPinRepository
{
    public PinRepository(AppDbContext context) : base(context)
    {
    }
}