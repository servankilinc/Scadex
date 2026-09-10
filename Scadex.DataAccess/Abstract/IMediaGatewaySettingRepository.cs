using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IMediaGatewaySettingRepository : IRepository<MediaGatewaySetting>, IRepositoryAsync<MediaGatewaySetting>
{
}
