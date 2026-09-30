using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.RemoteDesk.Model.Dtos.Pc.Queries;
using Scadex.RemoteDesk.Realtime;
using Scadex.RemoteDesk.Services.Abstract;
using Scadex.RemoteDesk.Streaming;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.RemoteDesk.Services.Concrete;

/// <summary> PC'ler çekirdekten YALNIZCA projeksiyonla okunur; modül çekirdek tablolarına yazmaz ve cihazı kopyalamaz. </summary>
public class PcDirectoryService : IPcDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PcConnectionRegistry _connections;
    private readonly ScreenStreamCoordinator _streams;

    public PcDirectoryService(IUnitOfWork unitOfWork, PcConnectionRegistry connections, ScreenStreamCoordinator streams)
    {
        _unitOfWork = unitOfWork;
        _connections = connections;
        _streams = streams;
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

        foreach (var pc in devices)
            await FillLiveAsync(pc, cancellationToken);

        return Result<ICollection<PcListItemDto>>.Success(devices);
    }

    /// <inheritdoc />
    public async Task<Result<PcDetailDto>> GetAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var pc = await _unitOfWork.Devices.GetAsync(
            select: d => new PcDetailDto
            {
                DeviceId = d.Id,
                DeviceName = d.Name,
                CabinetId = d.CabinetId,
                CabinetName = d.Cabinet!.Name,
                MacAddress = d.MacAddress
            },
            where: d => d.Id == deviceId && d.IsActive && d.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: cancellationToken);
        if (pc is null)
            return Result<PcDetailDto>.NotFound(message: "PC bulunamadı.");

        var streams = await FillLiveAsync(pc, cancellationToken);
        if (_connections.TryGet(deviceId, out var connection))
        {
            pc.MachineName = connection.MachineName;
            pc.UserName = connection.UserName;
            pc.OsVersion = connection.OsVersion;
            pc.ConnectedUtc = connection.ConnectedUtc;
            pc.Monitors = [.. connection.Monitors.OrderBy(m => m.Index).Select(m =>
            {
                var stream = streams.FirstOrDefault(s => s.Monitor == m.Index);
                return new PcMonitorDto
                {
                    Index = m.Index,
                    DeviceName = m.DeviceName,
                    Width = m.Width,
                    Height = m.Height,
                    IsPrimary = m.IsPrimary,
                    GpuName = m.GpuName,
                    StreamState = stream.State is { } state ? (int)state : null,
                    Encoder = stream.Encoder,
                    ViewerCount = stream.Viewers
                };
            })];
        }

        return Result<PcDetailDto>.Success(pc);
    }

    /// <summary> Bağlantı ve yayın durumu bellektedir (tabloya kopyalanmaz). </summary>
    private async Task<IReadOnlyList<(int Monitor, Contracts.Hub.ScreenStreamState? State, string? Encoder, int Viewers)>> FillLiveAsync(
        PcListItemDto pc, CancellationToken cancellationToken)
    {
        if (!_connections.TryGet(pc.DeviceId, out var connection))
            return [];

        pc.IsConnected = true;
        pc.ClientVersion = connection.ClientVersion;
        pc.MonitorCount = connection.Monitors.Count;

        var streams = await _streams.SnapshotAsync(pc.DeviceId, cancellationToken);
        pc.ViewerCount = streams.Sum(s => s.Viewers);
        return streams;
    }
}
