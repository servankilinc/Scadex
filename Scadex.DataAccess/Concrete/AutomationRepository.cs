using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class AutomationRepository : RepositoryBase<Automation, AppDbContext>, IAutomationRepository
{
    public AutomationRepository(AppDbContext context) : base(context)
    {
    }
}