using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class DeviceCommandRepository : RepositoryBase<DeviceCommand, AppDbContext>, IDeviceCommandRepository
{
    public DeviceCommandRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ICollection<DeviceCommand>> GetRecentForDeviceAsync(Guid deviceId, int take, CancellationToken cancellationToken = default)
    {
        // Siralama SentAt'e gore: satir zaten ancak gonderim aninda yaziliyor,
        // dolayisiyla bu alan her satirda dolu. Esitlikte Id kirilir ki ayni
        // milisaniyede yazilmis iki komut kararli bir sirada gelsin — aksi
        // halde sayfa her yenilendiginde sira degisebilirdi.
        return await _context.DeviceCommands
            .AsNoTracking()
            .Include(c => c.IoChannel)
            .Include(c => c.RequesterUser)
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.SentAt)
            .ThenByDescending(c => c.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
