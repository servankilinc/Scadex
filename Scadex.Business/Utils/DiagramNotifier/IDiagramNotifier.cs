using CabinetOs.Model.Dtos.Realtime.Queries;

namespace Scadex.Business.Utils.DiagramNotifier;

public interface IDiagramNotifier
{
    Task ChannelValuesChangedAsync(Guid cabinetId, IReadOnlyList<ChannelValueChange> changes, CancellationToken cancellationToken = default);

    Task DeviceStatusesChangedAsync(Guid cabinetId, IReadOnlyList<DeviceStatusChange> changes, CancellationToken cancellationToken = default);

    Task CabinetStatusChangedAsync(CabinetStatusChange change, CancellationToken cancellationToken = default);

    /// <summary>Bir komutun sonuclandigini ayni kabini izleyen DIGER kullanicilara bildirir. Komutu gonderen sonucu zaten HTTP yanitinda alir. </summary>
    Task CommandCompletedAsync(Guid cabinetId, CommandCompleted change, CancellationToken cancellationToken = default);
}
