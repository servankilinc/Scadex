using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class DeviceRepository : RepositoryBase<Device, AppDbContext>, IDeviceRepository
{
    public DeviceRepository(AppDbContext context) : base(context)
    {
    }
}