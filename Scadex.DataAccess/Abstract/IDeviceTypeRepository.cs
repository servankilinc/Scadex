using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IDeviceTypeRepository : IRepository<DeviceType>, IRepositoryAsync<DeviceType>
{
}