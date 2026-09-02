using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class IoChannelRepository : RepositoryBase<IoChannel, AppDbContext>, IIoChannelRepository
{
    public IoChannelRepository(AppDbContext context) : base(context)
    {
    }
}