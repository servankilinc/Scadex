using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class ComponentTemplatePinRepository : RepositoryBase<ComponentTemplatePin, AppDbContext>, IComponentTemplatePinRepository
{
    public ComponentTemplatePinRepository(AppDbContext context) : base(context)
    {
    }
}