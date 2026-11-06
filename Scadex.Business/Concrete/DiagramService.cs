using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Business.Utils;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Diagram.Queries;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public partial class DiagramService : IDiagramService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    private readonly ICameraProtocolProfileResolver _cameraProtocolProfileResolver;
    public DiagramService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper, ICameraProtocolProfileResolver cameraProtocolProfileResolver)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
        _cameraProtocolProfileResolver = cameraProtocolProfileResolver;
    }

    public async Task<Result<DiagramDto>> GetAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var automation = await _unitOfWork.Automations.GetAsync<DiagramAutomationDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.Id == automationId && c.IsActive,
            cancellationToken: cancellationToken
        );
        if (automation == null)
            return Result<DiagramDto>.NotFound(description: "Otomasyon bulunamadi veya pasif durumda");

        var devices = await _unitOfWork.Devices.GetAllAsync<DiagramDeviceDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: d => d.AutomationId == automationId && d.IsActive,
            orderBy: q => q.OrderBy(d => d.ZIndex).ThenBy(d => d.Name),
            cancellationToken: cancellationToken
        );

        var connectionRows = await _unitOfWork.Connections.GetAllAsync(
            select: c => new ConnectionRow(
                c.Id,
                c.AutomationId,
                c.SourcePinId,
                c.TargetPinId,
                c.SourcePin!.DeviceId,
                c.TargetPin!.DeviceId,
                c.Label,
                c.Color,
                c.LineStyle,
                c.StrokeWidth,
                c.Routing,
                c.WaypointsJson,
                c.ZIndex
            ),
            where: c =>
                c.AutomationId == automationId && c.IsDeleted == false && // otomasyonun silinmemiş pin bağlantıları
                c.SourcePin != null && c.SourcePin.IsDeleted == false && c.SourcePin.Device!.IsActive && // kaynak pin silinmemiş ve cihazı aktifse
                c.TargetPin != null && c.TargetPin.IsDeleted == false && c.TargetPin.Device!.IsActive,   // hedef pin silinmemiş ve cihazı aktifse
            orderBy: q => q.OrderBy(c => c.ZIndex),
            ignoreFilters: true,
            cancellationToken: cancellationToken);

        var connections = (connectionRows ?? [])
            .Select(r => new DiagramConnectionDto
            {
                Id = r.Id,
                AutomationId = r.AutomationId,
                SourcePinId = r.SourcePinId,
                TargetPinId = r.TargetPinId,
                SourceDeviceId = r.SourceDeviceId,
                TargetDeviceId = r.TargetDeviceId,
                Label = r.Label,
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
            where: a => a.AutomationId == automationId,
            orderBy: q => q.OrderBy(a => a.ZIndex),
            cancellationToken: cancellationToken
        );

        var canvasSettings = await _unitOfWork.CanvasSettings.GetAsync<DiagramCanvasSettingsDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: s => s.AutomationId == automationId,
            cancellationToken: cancellationToken
        );

        return Result<DiagramDto>.Success(new DiagramDto
        {
            Automation = automation,
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


    // ==================== YAZMA UÇLARININ ORTAK YARDIMCILARI ====================
    // Uçlar aggregate başına dosyalardadır: .Devices, .Connections, .Annotations, .Layout, .Delete.

    private Task<bool> IsAutomationActiveAsync(Guid automationId, CancellationToken cancellationToken) =>
        _unitOfWork.Automations.IsExistAsync(where: c => c.Id == automationId && c.IsActive, cancellationToken: cancellationToken);

    /// <summary> Ayni alanda birden fazla hata birikebilsin diye. <c>ProblemDetails.errors</c> sozlugunun sekli: alan -> mesaj dizisi. </summary>
    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToFailures(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);

    /// <summary> <c>Waypoints</c> JSON string dönüşümü SQL'e cevrilemeyeceği için ara bir model açtık </summary>
    private sealed record ConnectionRow(
        Guid Id,
        Guid AutomationId,
        Guid SourcePinId,
        Guid TargetPinId,
        Guid SourceDeviceId,
        Guid TargetDeviceId,
        string? Label,
        string Color,
        LineStyle LineStyle,
        double StrokeWidth,
        EdgeRouting Routing,
        string? WaypointsJson,
        int ZIndex
    );
}
