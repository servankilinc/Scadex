using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;

namespace Scadex.Business.Abstract;

public interface IDeviceCommandService
{
    Task<Result<DeviceCommandResultDto>> SendAsync(Guid deviceId, DeviceCommandSendRequest request, CancellationToken cancellationToken = default);
    /// <summary>Cihazin son komutları, yeniden eskiye.</summary>
    Task<Result<ICollection<DeviceCommandResultDto>>> GetRecentAsync(Guid deviceId, int take, CancellationToken cancellationToken = default);
}