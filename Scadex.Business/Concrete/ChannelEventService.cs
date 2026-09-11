using AutoMapper;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.ScadaEvents;
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
    private readonly ILogger<ChannelEventService> _logger;
    private readonly IMapper _mapper;
    private readonly IEnumerable<IScadaEventObserver> _observers;

    public ChannelEventService(IUnitOfWork unitOfWork, IValidationService validationService, IDiagramNotifier notifier, ILogger<ChannelEventService> logger, IMapper mapper, IEnumerable<IScadaEventObserver> observers)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _notifier = notifier;
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


        // 5) Kanal kontrolü
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
            return Result.Success();
        }


        // 6) Cihaz(Component) kontrolü
        var device = await _unitOfWork.Devices.GetAsync(
            where: d => d.Id == channel.DeviceId,
            tracking: true,
            cancellationToken: cancellationToken
        );
        if (device == null)
        {
            _logger.LogWarning($"Kabin {cabinet.Id}: Cihaz({channel.DeviceId}) bulunamadi; telemetri atlandi.");
            return Result.Success();
        }


        // 7) Cihaz durum değişimi kontrolü
        var statusChanges = new List<DeviceStatusChange>();
        bool deviceStatusChanged = false;

        var now = DateTime.UtcNow;
        device.LastSeen = now;

        var previousStatus = device.DeviceStatusId;
        // NOT: Cihazin yeni durumu — eğer daha once offline ise online'a cekilir; degilse ayni kalir(hata vb. durumların da ise kaybolmaması gerekiyor).
        var nextStatus = previousStatus == null || previousStatus == (int)DeviceStatus.Offline ? (int)DeviceStatus.Online : previousStatus;

        deviceStatusChanged = nextStatus != previousStatus;
        if (deviceStatusChanged)
        {
            device.DeviceStatusId = nextStatus;
            statusChanges.Add(new DeviceStatusChange
            {
                DeviceId = device.Id,
                StatusId = (DeviceStatus?)nextStatus,
                LastSeen = now
            });
        }


        // 8) Kabin bilgilerinin güncellenmesi: kabin durumu, kabindeki cihazların en kotü durumuna göre belirlenir.
        if (deviceStatusChanged)
        {
            // kabinin cihazlarının durumlarını alıp kabin status bilgisi için en kotü durumu bulunur.
            var statuses = await _unitOfWork.Devices.GetAllAsync(
                select: d => d.DeviceStatusId,
                where: d => d.CabinetId == cabinet.Id && d.IsActive && d.Id != device!.Id,
                cancellationToken: cancellationToken
            ) ?? [];

            cabinet.DeviceStatusId = CalculateWorstStatus([.. statuses, device.DeviceStatusId]);
        }
        cabinet.LastSeen = now;
        cabinet.ScadaLastIngestAt = now;


        // 9) Kanal değeri değişimi kontrolü ve değeri değişen kanallar için ChannelEvent insert ve client'lara bildirim.
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


        // 10) Değişiklikler kalıcı olarak yazılır
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        // 11) kalıcı olarak yazılan olaylar ve durum degisiklikleri, client'lara bildirilir.
        if (channelChanges.Count > 0)
            await _notifier.ChannelValuesChangedAsync(cabinet.Id, channelChanges, cancellationToken);

        if (statusChanges.Count > 0)
            await _notifier.DeviceStatusesChangedAsync(cabinet.Id, statusChanges, cancellationToken);

        // Kabin "StatusId" degismese bile govdesindeki "ScadaLastIngestAt", "LastSeen" değiştiği için bildirilir.
        await _notifier.CabinetStatusChangedAsync(new CabinetStatusChange
        {
            CabinetId = cabinet.Id,
            StatusId = (DeviceStatus?)cabinet.DeviceStatusId,
            LastSeen = cabinet.LastSeen,
            ScadaLastIngestAt = cabinet.ScadaLastIngestAt
        }, cancellationToken);

        // 12) Feğişen kanal, Scadex'in dış modüllerine (dinleyicilere) yayınlanır
        if (observerNotification != null)
            await _observers.PublishAsync(o => o.OnChannelChangedAsync(observerNotification, cancellationToken), _logger, nameof(IScadaEventObserver.OnChannelChangedAsync));

        return Result.Success();
    }


    /// <inheritdoc />
    public async Task<int> SetOfflineDevicesAsync(TimeSpan staleAfter, CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow - staleAfter;


        // 1) Haber alınamayan cihazları bulunan kabinleri tespit et
        var staleCabinetIds = await _unitOfWork.Devices.GetAllAsync(
            select: d => d.CabinetId,
            where: d =>
                d.IsActive &&
                d.LastSeen != null &&
                d.LastSeen < threshold &&
                d.DeviceStatusId != (int)DeviceStatus.Offline,
            cancellationToken: cancellationToken
        ) ?? [];
        var cabinetIds = staleCabinetIds.Distinct().ToList();
        if (cabinetIds.Count == 0)
            return 0;


        // 2) İlgili kabinleri ve kabinlerin tüm cihazlarını çek
        var cabinets = await _unitOfWork.Cabinets.GetAllAsync(
            where: c => cabinetIds.Contains(c.Id),
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];

        var devices = await _unitOfWork.Devices.GetAllAsync(
            where: d => cabinetIds.Contains(d.CabinetId) && d.IsActive,
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];


        // 3) Haber alınamayan cihazları Offline'a çek
        var changesByCabinet = new Dictionary<Guid, List<DeviceStatusChange>>();
        int swept = 0;

        foreach (var device in devices)
        {
            bool isStale = device.LastSeen != null && device.LastSeen < threshold && device.DeviceStatusId != (int)DeviceStatus.Offline;
            if (!isStale)
                continue;

            device.DeviceStatusId = (int)DeviceStatus.Offline;
            swept++;

            if (!changesByCabinet.TryGetValue(device.CabinetId, out var list))
                changesByCabinet[device.CabinetId] = list = [];

            list.Add(new DeviceStatusChange
            {
                DeviceId = device.Id,
                StatusId = DeviceStatus.Offline,
                LastSeen = device.LastSeen
            });
        }
        if (swept == 0)
            return 0;


        // 4) Kabinlerin cihaz durumlarını güncelle
        var devicesByCabinet = devices.GroupBy(d => d.CabinetId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var cabinet in cabinets)
        {
            if (devicesByCabinet.TryGetValue(cabinet.Id, out var cabinetDevices))
                cabinet.DeviceStatusId = CalculateWorstStatus(cabinetDevices.Select(d => d.DeviceStatusId));
        }

        // 5) Değişiklikleri kalıcı olarak yaz
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        // 6) Değişiklikleri client'lara bildir
        foreach (var (cabinetId, changes) in changesByCabinet)
        {
            await _notifier.DeviceStatusesChangedAsync(cabinetId, changes, cancellationToken);

            var cabinet = cabinets.FirstOrDefault(c => c.Id == cabinetId);
            if (cabinet == null)
                continue;

            await _notifier.CabinetStatusChangedAsync(new CabinetStatusChange
            {
                CabinetId = cabinet.Id,
                StatusId = (DeviceStatus?)cabinet.DeviceStatusId,
                LastSeen = cabinet.LastSeen,
                ScadaLastIngestAt = cabinet.ScadaLastIngestAt
            }, cancellationToken);
        }

        return swept;
    }

    #region Helpers
    /// <summary> Bir kabinin cihaz durumları içerisinden en kotü olanı bulup döndürür. </summary>
    private static int? CalculateWorstStatus(IEnumerable<int?> statuses)
    {
        int? worst = null;
        int worstRank = -1;

        foreach (var candidate in statuses)
        {
            if (candidate is not int status)
                continue;
            int rank = Model.Enums.EntityEnums.DeviceStatusSeverityRank(status);
            if (rank <= worstRank)
                continue;
            worstRank = rank;
            worst = status;
        }

        return worst;
    }
    #endregion
}
