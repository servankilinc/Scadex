using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IDeviceRepository : IRepository<Device>, IRepositoryAsync<Device>
{
    Task<Guid> GetCabinetIdByControlModuleMacAsync(string macAddress, CancellationToken cancellationToken = default);
}
