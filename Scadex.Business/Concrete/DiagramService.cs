using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Business.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Diagram.Queries;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public partial class DiagramService : IDiagramService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public DiagramService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    public async Task<Result<DiagramDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default)
    {
        var cabinet = await _unitOfWork.Cabinets.GetAsync<DiagramCabinetDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.Id == cabinetId && c.IsActive,
            cancellationToken: cancellationToken
        );
        if (cabinet == null)
            return Result<DiagramDto>.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var devices = await _unitOfWork.Devices.GetAllAsync<DiagramDeviceDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: d => d.CabinetId == cabinetId && d.IsActive,
            orderBy: q => q.OrderBy(d => d.ZIndex).ThenBy(d => d.Name),
            cancellationToken: cancellationToken
        );

        var connectionRows = await _unitOfWork.Connections.GetAllAsync(
            select: c => new ConnectionRow(
                c.Id,
                c.CabinetId,
                c.SourcePinId,
                c.TargetPinId,
                c.SourcePin!.DeviceId,
                c.TargetPin!.DeviceId,
                c.Label,
                c.WireType,
                c.Color,
                c.LineStyle,
                c.StrokeWidth,
                c.Routing,
                c.WaypointsJson,
                c.ZIndex
            ),
            where: c =>
                c.CabinetId == cabinetId && c.IsDeleted == false && // kabinin silinmemiş pin bağlantıları
                c.SourcePin != null && c.SourcePin.IsDeleted != false && c.SourcePin.Device!.IsActive && // kaynak pin silinmemeiş ve cihazı aktifse
                c.TargetPin != null && c.TargetPin.IsDeleted != false && c.TargetPin.Device!.IsActive,   // hedef pin silinmemeiş ve cihazı aktifse
            orderBy: q => q.OrderBy(c => c.ZIndex),
            ignoreFilters: true,
            cancellationToken: cancellationToken);

        var connections = (connectionRows ?? [])
            .Select(r => new DiagramConnectionDto
            {
                Id = r.Id,
                CabinetId = r.CabinetId,
                SourcePinId = r.SourcePinId,
                TargetPinId = r.TargetPinId,
                SourceDeviceId = r.SourceDeviceId,
                TargetDeviceId = r.TargetDeviceId,
                Label = r.Label,
                WireType = r.WireType,
                Color = r.Color,
                LineStyle = r.LineStyle,
                StrokeWidth = r.StrokeWidth,
                Routing = r.Routing,
                Waypoints = DiagramWaypoints.Parse(r.WaypointsJson),
                ZIndex = r.ZIndex
            })
            .ToList();

        var annotations = await _unitOfWork.DiagramAnnotations.GetAllAsync<DiagramAnnotationItemDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: a => a.CabinetId == cabinetId,
            orderBy: q => q.OrderBy(a => a.ZIndex),
            cancellationToken: cancellationToken
        );

        var canvasSettings = await _unitOfWork.CanvasSettings.GetAsync<DiagramCanvasSettingsDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: s => s.CabinetId == cabinetId,
            cancellationToken: cancellationToken
        );

        return Result<DiagramDto>.Success(new DiagramDto
        {
            Cabinet = cabinet,
            Devices = devices ?? [],
            Connections = connections,
            Annotations = annotations ?? [],
            CanvasSettings = canvasSettings ?? new()
            {
                GridSize = 20,
                SnapToGrid = true,
                BackgroundVariant = BackgroundVariant.Dots,
                GridColor = "#E2E8F0",
                BackgroundColor = "#FFFFFF",
                MinZoom = 0.2,
                MaxZoom = 4
            },
            FetchedAtUtc = DateTime.UtcNow
        });
    }


    /// <summary> <c>Waypoints</c> JSON string dönüşümü SQL'e cevrilemeyeceği için ara bir model açtık </summary>
    private sealed record ConnectionRow(
        Guid Id,
        Guid CabinetId,
        Guid SourcePinId,
        Guid TargetPinId,
        Guid SourceDeviceId,
        Guid TargetDeviceId,
        string? Label,
        WireType WireType,
        string Color,
        LineStyle LineStyle,
        double StrokeWidth,
        EdgeRouting Routing,
        string? WaypointsJson,
        int ZIndex
    );
}
