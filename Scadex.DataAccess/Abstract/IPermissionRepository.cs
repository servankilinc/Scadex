using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IPermissionRepository : IRepository<Permission>, IRepositoryAsync<Permission>
{
}