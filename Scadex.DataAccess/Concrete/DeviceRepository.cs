using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.DataAccess.Concrete;

public class DeviceRepository : RepositoryBase<Device, AppDbContext>, IDeviceRepository
{
    public DeviceRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Guid> GetCabinetIdByControlModuleMacAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        return await _context.ControlCards
            .AsNoTracking()
            .Where(c =>
                c.MacAddress == macAddress &&
                c.Device!.IsActive &&
                c.Device.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule)
            .Select(c => c.Device!.CabinetId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
