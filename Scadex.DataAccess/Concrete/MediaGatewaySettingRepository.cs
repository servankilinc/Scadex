using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class MediaGatewaySettingRepository : RepositoryBase<MediaGatewaySetting, AppDbContext>, IMediaGatewaySettingRepository
{
    public MediaGatewaySettingRepository(AppDbContext context) : base(context)
    {
    }
}
