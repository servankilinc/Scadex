using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IUserRepository : IRepository<User>, IRepositoryAsync<User>
{
}