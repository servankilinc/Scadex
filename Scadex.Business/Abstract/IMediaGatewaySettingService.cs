using Scadex.Business.Settings;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.MediaGatewaySetting.Commands;
using Scadex.Model.Dtos.MediaGatewaySetting.Queries;

namespace Scadex.Business.Abstract;

public interface IMediaGatewaySettingService
{
    Task<MediaGatewaySettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<MediaGatewaySettingDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(MediaGatewaySettingUpdateDto request, CancellationToken cancellationToken = default);
}
