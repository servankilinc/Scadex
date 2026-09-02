using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IDeviceCommandRepository : IRepository<DeviceCommand>, IRepositoryAsync<DeviceCommand>
{
    /// <summary> Bir cihazin en son kumandalari, yeniden eskiye. </summary>
    Task<ICollection<DeviceCommand>> GetRecentForDeviceAsync(Guid deviceId, int take, CancellationToken cancellationToken = default);
}
