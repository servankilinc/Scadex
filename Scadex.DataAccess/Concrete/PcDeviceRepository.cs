using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class PcDeviceRepository : RepositoryBase<PcDevice, AppDbContext>, IPcDeviceRepository
{
    public PcDeviceRepository(AppDbContext context) : base(context)
    {
    }
}
