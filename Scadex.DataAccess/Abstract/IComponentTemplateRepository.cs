using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IComponentTemplateRepository : IRepository<ComponentTemplate>, IRepositoryAsync<ComponentTemplate>
{
}