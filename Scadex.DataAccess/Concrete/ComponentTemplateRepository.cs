using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class ComponentTemplateRepository : RepositoryBase<ComponentTemplate, AppDbContext>, IComponentTemplateRepository
{
    public ComponentTemplateRepository(AppDbContext context) : base(context)
    {
    }
}