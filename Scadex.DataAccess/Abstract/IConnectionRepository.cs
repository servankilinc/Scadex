using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IConnectionRepository : IRepository<Connection>, IRepositoryAsync<Connection>
{
}