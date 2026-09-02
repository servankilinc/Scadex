using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class DeviceStatusRepository : RepositoryBase<DeviceStatus, AppDbContext>, IDeviceStatusRepository
{
    public DeviceStatusRepository(AppDbContext context) : base(context)
    {
    }
}