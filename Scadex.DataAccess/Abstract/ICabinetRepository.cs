using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface ICabinetRepository : IRepository<Cabinet>, IRepositoryAsync<Cabinet>
{
}