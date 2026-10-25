using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class ControlCardRepository : RepositoryBase<ControlCard, AppDbContext>, IControlCardRepository
{
    public ControlCardRepository(AppDbContext context) : base(context)
    {
    }
}
