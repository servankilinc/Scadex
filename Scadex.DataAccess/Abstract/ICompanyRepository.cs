using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface ICompanyRepository : IRepository<Company>, IRepositoryAsync<Company>
{
}