using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Model.Dtos.Pc.Queries;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Services.Abstract;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk.Services.Concrete;

/// <summary> PC'ler çekirdekten YALNIZCA projeksiyonla okunur; modül çekirdek tablolarına yazmaz ve cihazı kopyalamaz. </summary>
public class PcDirectoryService : IPcDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PcConnectionRegistry _connections;

    public PcDirectoryService(IUnitOfWork unitOfWork, PcConnectionRegistry connections)
    {
        _unitOfWork = unitOfWork;
        _connections = connections;
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<PcListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _unitOfWork.Devices.GetAllAsync(
            select: d => new PcListItemDto
            {
                DeviceId = d.Id,
                DeviceName = d.Name,
                CabinetId = d.CabinetId,
                CabinetName = d.Cabinet!.Name,
                MacAddress = d.MacAddress
            },
            where: d => d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            orderBy: q => q.OrderBy(d => d.Cabinet!.Name).ThenBy(d => d.Name),
            cancellationToken: cancellationToken) ?? [];

        // Bağlantı durumu bellektedir (tabloya kopyalanmaz).
        foreach (var pc in devices)
        {
            if (!_connections.TryGet(pc.DeviceId, out var connection))
                continue;

            pc.IsConnected = true;
            pc.ClientVersion = connection.ClientVersion;
            pc.MonitorCount = connection.Monitors.Count;
        }

        return Result<ICollection<PcListItemDto>>.Success(devices);
    }
}
