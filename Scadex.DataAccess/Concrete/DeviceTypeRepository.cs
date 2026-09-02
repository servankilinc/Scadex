using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class DeviceTypeRepository : RepositoryBase<DeviceType, AppDbContext>, IDeviceTypeRepository
{
    public DeviceTypeRepository(AppDbContext context) : base(context)
    {
    }
}