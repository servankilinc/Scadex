using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IRoleRepository : IRepository<Role>, IRepositoryAsync<Role>
{
}