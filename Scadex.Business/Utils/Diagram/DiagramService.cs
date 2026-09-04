using AutoMapper;
using CabinetOs.Business.Utils;
using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Core.Utils.Validation;
using CabinetOs.DataAccess.UoW;
using CabinetOs.Model.Dtos.Diagram.Queries;
using CabinetOs.Model.Dtos.Diagram.Queries.Items;
using static CabinetOs.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.Diagram;

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
            cancellationToken: cancellationToken);

        if (cabinet == null)
            return Result<DiagramDto>.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        // Sablon ozeti cihazla birlikte tasinir (sablon pasife alinsa bile kabin
        // dogru boyut ve renkle render olmali); Pin ve IoChannel ISoftDeletableEntity
        // oldugu icin silinmis satirlari global query filter zaten eliyor.
        var devices = await _unitOfWork.Devices.GetAllAsync<DiagramDeviceDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: d => d.CabinetId == cabinetId && d.IsActive,
            orderBy: q => q.OrderBy(d => d.ZIndex).ThenBy(d => d.Name),
            cancellationToken: cancellationToken);

        // Kablolar: WaypointsJson bellek icinde ayristirilir (JSON okuma SQL'e
        // cevrilemez), bu yuzden once ara bir satir sekline projekte edilir.
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
                c.ZIndex),
            // Savunmaci eleme. Iki ayri bozulma yolu var ve ikisi de React Flow'da
            // "var olmayan node'a bagli edge" hatasi uretir:
            //   1) Pin soft-delete edilmis  -> query filter pini gizler ama kablo ayakta kalir,
            //      navigasyon NULL olur ve DeviceId projeksiyonu patlardi.
            //   2) Cihaz pasife alinmis     -> pin durur ama cihaz devices[] listesinde yoktur.
            where: c => c.CabinetId == cabinetId
                     && c.SourcePin != null && c.TargetPin != null
                     && c.SourcePin.Device!.IsActive && c.TargetPin.Device!.IsActive,
            orderBy: q => q.OrderBy(c => c.ZIndex),
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
            cancellationToken: cancellationToken);

        var canvasSettings = await _unitOfWork.CanvasSettings.GetAsync<DiagramCanvasSettingsDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: s => s.CabinetId == cabinetId,
            cancellationToken: cancellationToken);

        return Result<DiagramDto>.Success(new DiagramDto
        {
            Cabinet = cabinet,
            Devices = devices ?? [],
            Connections = connections,
            Annotations = annotations ?? [],
            // Kayitli ayar yoksa VARSAYILAN doner ve satir OLUSTURULMAZ: bir kabini
            // yalnizca acmak veritabanina yazmamali.
            CanvasSettings = canvasSettings ?? CreateDefaultCanvasSettings(),
            FetchedAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Kayitli ayari olmayan kabinin varsayilanlari. Bu degerler sozlesmenin parcasidir
    /// degistirilirse mevcut kabinlerin gorunumu sessizce degisir.
    /// </summary>
    private static DiagramCanvasSettingsDto CreateDefaultCanvasSettings() => new()
    {
        GridSize = 20,
        SnapToGrid = true,
        BackgroundVariant = BackgroundVariant.Dots,
        GridColor = "#E2E8F0",
        BackgroundColor = "#FFFFFF",
        MinZoom = 0.2,
        MaxZoom = 4
    };

    /// <summary>
    /// Kablo satirinin ara sekli. DTO'ya dogrudan projekte edemiyoruz cunku
    /// <c>Waypoints</c> bir JSON string'inden turer ve bu SQL'e cevrilemez.
    /// </summary>
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
        int ZIndex);
}
