using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IPinRepository : IRepository<Pin>, IRepositoryAsync<Pin>
{
}