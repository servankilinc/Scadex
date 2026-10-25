using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IControlCardRepository : IRepository<ControlCard>, IRepositoryAsync<ControlCard>
{
}
