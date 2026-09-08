using Scadex.Model.Dtos.Realtime.Queries;

namespace Scadex.WebAPI.Hubs;

/// <summary> Hub'in Client arayüzü, Client'lara hangi mesajların gönderilebileceğini tanımlıyorum. </summary>
public interface IDiagramHubClientContract
{
    Task ChannelValuesChanged(Guid cabinetId, IReadOnlyList<ChannelValueChange> changes);

    Task DeviceStatusChanged(Guid cabinetId, IReadOnlyList<DeviceStatusChange> changes);

    Task CabinetStatusChanged(CabinetStatusChange change);

    Task CommandCompleted(Guid cabinetId, CommandCompleted change);
}
