using AutoMapper;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.ScadaObserver;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Model.Entities;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using PinDirection = Scadex.Model.Enums.EntityEnums.PinDirection;

namespace Scadex.Business.Concrete;

public class ChannelEventService : IChannelEventService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IDiagramNotifier _notifier;
    private readonly ICabinetStatusService _cabinetStatusService;
    private readonly ILogger<ChannelEventService> _logger;
    private readonly IMapper _mapper;
    private readonly IEnumerable<IScadaEventObserver> _observers;

    public ChannelEventService(IUnitOfWork unitOfWork, IValidationService validationService, IDiagramNotifier notifier, ICabinetStatusService cabinetStatusService, ILogger<ChannelEventService> logger, IMapper mapper, IEnumerable<IScadaEventObserver> observers)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _notifier = notifier;
        _cabinetStatusService = cabinetStatusService;
        _logger = logger;
        _mapper = mapper;
        _observers = observers;
    }

    /// <inheritdoc />
    public async Task<Result<PaginationResponse<ChannelEventDto>>> GetPagedAsync(ChannelEventQueryRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<PaginationResponse<ChannelEventDto>>.Validation(validationResult.Failures, description: "Validation failed for ChannelEventQueryRequest");

        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(
            where: c => c.Id == request.CabinetId,
            cancellationToken: cancellationToken
        );

        if (!cabinetExists)
            return Result<PaginationResponse<ChannelEventDto>>.NotFound(description: "Kabin bulunamadi");

        var page = await _unitOfWork.ChannelEvents.GetPagedAsync(
            _mapper.ConfigurationProvider,
            request.CabinetId,
            request.IoChannelId,
            request.FromUtc,
            request.ToUtc,
            request.ToPaginationRequest(),
            cancellationToken);

        return Result<PaginationResponse<ChannelEventDto>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result> IngestAsync(ScadaIngestRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ScadaIngestRequest");


        // 2) Ayrıştırma kontrolü
        if (!ScadaPinAddress.CheckAndParseIngestPin(request.Type, out var direction))
            return Result.Failure("Gecersiz tip", "Invalid signal type");

        // 3) SCADA MAC adresiyle eslesen kontrol modülünden bulunur
        var cabinetId = await _unitOfWork.Devices.GetCabinetIdByControlModuleMacAsync(request.MacAddress, cancellationToken);
        if (cabinetId == Guid.Empty)
        {
            _logger.LogWarning($"MAC {request.MacAddress}: eslesen kontrol modulu yok (ya da pasif); telemetri atlandi.");
            return Result.NotFound(description: "Bu MAC adresine kayitli kontrol modulu bulunamadi");
        }


        // 4) Kabin kontrolü
        var cabinet = await _unitOfWork.Cabinets.GetAsync(
            where: c => c.Id == cabinetId && c.IsActive,
            tracking: true,
            cancellationToken: cancellationToken
        );
        if (cabinet == null)
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");
        if (!cabinet.ScadaIsEnabled)
            return Result.Failure($"Bu kabinde({cabinet.Id}) SCADA kapalı.");


        // 5) Kart haberleşiyor canlılık kanıtıdır. Kontrol modülünü (SCADA kartı) Online'a çek, Cabinet.LastSeen'i yenile, gerekirse kabini yeniden hesaplar.
        var now = DateTime.UtcNow;
        cabinet.ScadaLastIngestAt = now;
        await _cabinetStatusService.RecordScadaContactAsync(cabinet.Id, cancellationToken);


        // 6) Kanal kontrolü
        var channel = await _unitOfWork.IoChannels.GetAsync(
            where: c =>
                c.CabinetId == cabinet.Id &&
                c.Direction == direction &&
                c.ChannelNumber == request.ChannelNumber &&
                c.IsEnabled,
            tracking: true,
            cancellationToken: cancellationToken
        );
        if (channel == null)
        {
            _logger.LogWarning($"Kabin {cabinet.Id}: {ScadaPinAddress.Format(direction, request.ChannelNumber)} pini tanimsiz (ya da devre disi); telemetri atlandi.");
            await PublishCabinetHeartbeatAsync(cabinet, cancellationToken);
            return Result.Success();
        }


        // 7) Cihaz(Component) kontrolü
        var deviceExists = await _unitOfWork.Devices.IsExistAsync(
            where: d => d.Id == channel.DeviceId,
            cancellationToken: cancellationToken
        );
        if (!deviceExists)
        {
            _logger.LogWarning($"Kabin {cabinet.Id}: Cihaz({channel.DeviceId}) bulunamadi; telemetri atlandi.");
            await PublishCabinetHeartbeatAsync(cabinet, cancellationToken);
            return Result.Success();
        }


        // 8) Kanal değeri değişimi kontrolü ve değeri değişen kanallar için ChannelEvent insert ve client'lara bildirim.
        var channelChanges = new List<ChannelValueChange>();
        ChannelEvent? channelEvent = null;
        ChannelChangedNotification? observerNotification = null;

        if (!string.Equals(channel.CurrentValue, request.Value, StringComparison.Ordinal))
        {
            var previousValue = channel.CurrentValue;

            // Eğer dinleyicilere varsa iletilecek bilgiler
            observerNotification = new ChannelChangedNotification
            {
                CabinetId = cabinet.Id,
                IoChannelId = channel.Id,
                DeviceId = channel.DeviceId,
                Direction = channel.Direction,
                ChannelNumber = channel.ChannelNumber,
                Value = request.Value,
                PreviousValue = previousValue,
                OccurredAtUtc = request.TimestampUtc ?? now,
                ReceivedAtUtc = now
            };

            channel.CurrentValue = request.Value;
            channel.ValueUpdatedAt = now;

            channelChanges.Add(new ChannelValueChange
            {
                IoChannelId = channel.Id,
                DeviceId = channel.DeviceId,
                ChannelNumber = channel.ChannelNumber,
                Value = request.Value,
                UpdatedAt = now
            });

            // Input ve Analaog input pinleri için ChannelEvent(telemetri) yazılır
            if ((channel.Direction == PinDirection.Input || channel.Direction == PinDirection.AnalogInput)
                && request.Value != null)
            {
                channelEvent = new ChannelEvent
                {
                    IoChannelId = channel.Id,
                    CabinetId = cabinet.Id,
                    Value = request.Value!,
                    PreviousValue = previousValue,
                    OccurredAtUtc = request.TimestampUtc ?? now,
                    ReceivedAtUtc = now
                };

                _unitOfWork.ChannelEvents.Add(channelEvent);
            }
        }


        // 9) Değişiklikler kalıcı olarak yazılır
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        // 10) kalıcı olarak yazılan olaylar client'lara bildirilir
        if (channelChanges.Count > 0)
            await _notifier.ChannelValuesChangedAsync(cabinet.Id, channelChanges, cancellationToken);

        await PublishCabinetHeartbeatAsync(cabinet, cancellationToken);

        // 11) Değişen kanal, Scadex'in dış modüllerine (dinleyicilere) yayınlanır
        if (observerNotification != null)
            await _observers.PublishAsync(o => o.OnChannelChangedAsync(observerNotification, cancellationToken), _logger, nameof(IScadaEventObserver.OnChannelChangedAsync));

        return Result.Success();
    }

    #region Helpers
    /// <summary>
    /// Kabin "StatusId" degismese bile govdesindeki "ScadaLastIngestAt", "LastSeen" her ingest'te degistigi icin kabini
    /// izleyenlere (diyagram) bildirilir. Tum kabinler grubuna GITMEZ — o grup yalnizca durum degisince haberdar edilir.
    /// </summary>
    private Task PublishCabinetHeartbeatAsync(Cabinet cabinet, CancellationToken cancellationToken) =>
        _notifier.CabinetStatusChangedAsync(new CabinetStatusChange
        {
            CabinetId = cabinet.Id,
            StatusId = (DeviceStatus?)cabinet.DeviceStatusId,
            LastSeen = cabinet.LastSeen,
            ScadaLastIngestAt = cabinet.ScadaLastIngestAt
        }, cancellationToken);
    #endregion
}
