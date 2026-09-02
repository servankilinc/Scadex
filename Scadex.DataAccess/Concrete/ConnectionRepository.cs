using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class ConnectionRepository : RepositoryBase<Connection, AppDbContext>, IConnectionRepository
{
    public ConnectionRepository(AppDbContext context) : base(context)
    {
    }
}