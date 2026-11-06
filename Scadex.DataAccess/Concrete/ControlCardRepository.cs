using Microsoft.EntityFrameworkCore;
using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;
using EntityEnums = Scadex.Model.Enums.EntityEnums;

namespace Scadex.DataAccess.Concrete;

public class ControlCardRepository : RepositoryBase<ControlCard, AppDbContext>, IControlCardRepository
{
    public ControlCardRepository(AppDbContext context) : base(context)
    {
    }

    public Task<ControlCard?> GetActiveForAutomationAsync(Guid automationId, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ControlCards.Where(c =>
            c.Device!.AutomationId == automationId &&
            c.Device.IsActive &&
            c.Device.ComponentTemplate!.DeviceTypeId == (int)EntityEnums.DeviceType.ControlModule);

        return (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);
    }
}
