using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IIoChannelRepository : IRepository<IoChannel>, IRepositoryAsync<IoChannel>
{
}