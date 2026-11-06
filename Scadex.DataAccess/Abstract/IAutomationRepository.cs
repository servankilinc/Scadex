using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IAutomationRepository : IRepository<Automation>, IRepositoryAsync<Automation>
{
}