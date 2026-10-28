using AutoMapper;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.OutputPolarity;
using Scadex.Business.Utils.ScadaCommandGateway;
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
using ChannelChangeSource = Scadex.Model.Enums.EntityEnums.ChannelChangeSource;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;
using PinDirection = Scadex.Model.Enums.EntityEnums.PinDirection;

namespace Scadex.Business.Concrete;

public partial class ChannelEventService : IChannelEventService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IDiagramNotifier _notifier;
    private readonly ICabinetStatusService _cabinetStatusService;
    private readonly ILogger<ChannelEventService> _logger;
    private readonly IMapper _mapper;
    private readonly IEnumerable<IScadaEventObserver> _observers;
    private readonly IOutputPolarityResolver _polarityResolver;
    private readonly IScadaCommandGateway _scadaCommandGateway;

    public ChannelEventService(IUnitOfWork unitOfWork, IValidationService validationService, IDiagramNotifier notifier, ICabinetStatusService cabinetStatusService, ILogger<ChannelEventService> logger, IMapper mapper, IEnumerable<IScadaEventObserver> observers, IOutputPolarityResolver polarityResolver, IScadaCommandGateway scadaCommandGateway)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _notifier = notifier;
        _cabinetStatusService = cabinetStatusService;
        _logger = logger;
        _mapper = mapper;
        _observers = observers;
        _polarityResolver = polarityResolver;
        _scadaCommandGateway = scadaCommandGateway;
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
    public async Task<Result> IngestAsync(Guid cabinetId, ScadaIngestRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ScadaIngestRequest");


        // 2) Ayrıştırma kontrolü
        if (!ScadaPinAddress.CheckAndParseIngestPin(request.Type, out var direction))
            return Result.Failure("Gecersiz tip", "Invalid signal type");

        // 3) Kabin, isteği gönderen kontrol kartının kimliğinden gelir (DeviceId/MAC header'ı, controller); burada yalnızca durumu denetlenir.
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


        // 8) Değer değiştiyse kanala yazılır, ChannelEvent eklenir, client'lara ve dış modüllere bildirilir. Kabindeki ScadaLastIngestAt da aynı SaveChanges'te yazılır.
        await ApplyChannelValuesAsync(cabinet.Id, [new ChannelWrite(channel, request.Value, request.TimestampUtc ?? now)], ChannelChangeSource.Ingest, now, cancellationToken);

        // 9) Kabin gövdesi (ScadaLastIngestAt, LastSeen) her ingest'te değişir
        await PublishCabinetHeartbeatAsync(cabinet, cancellationToken);

        return Result.Success();
    }

    #region Helpers
    /// <summary> Kanala yazılacak değer: <see cref="OccurredAtUtc"/> sahada gerçekleştiği an (bilinmiyorsa fark edildiği an). </summary>
    private sealed record ChannelWrite(IoChannel Channel, string? Value, DateTime OccurredAtUtc);

    /// <summary>
    /// Takipli kanallara değerleri yazar. Yalnızca DEĞİŞEN kanal için: <c>CurrentValue</c> + <c>ValueUpdatedAt</c>, giriş/analogda
    /// <c>ChannelEvent</c> satırı, kaydettikten SONRA diyagrama <c>ChannelValuesChanged</c> ve gözlemcilere <c>OnChannelChangedAsync</c>.
    /// Değişen yoksa da <c>SaveChanges</c> çağrılır: çağıranın takipli diğer değişiklikleri (ör. <c>ScadaLastIngestAt</c>) yazılsın.
    /// </summary>
    /// <returns> Değişen kanal sayısı. </returns>
    private async Task<int> ApplyChannelValuesAsync(Guid cabinetId, IReadOnlyCollection<ChannelWrite> writes, ChannelChangeSource source, DateTime now, CancellationToken cancellationToken)
    {
        var changed = writes.Where(w => !string.Equals(w.Channel.CurrentValue, w.Value, StringComparison.Ordinal)).ToList();

        // Çıkışta gözlemciye MANTIKSAL değer (TurnOn) gider: karttaki fiziksel değer NO/NC'ye göre çevrilir.
        var outputIds = changed.Where(w => w.Channel.Direction == PinDirection.Output && w.Value != null).Select(w => w.Channel.Id).ToList();
        var polarities = outputIds.Count == 0
            ? new Dictionary<Guid, PolarityResolution>()
            : await _polarityResolver.ResolveManyAsync(outputIds, cancellationToken);

        var valueChanges = new List<ChannelValueChange>(changed.Count);
        var notifications = new List<ChannelChangedNotification>(changed.Count);

        foreach (var (channel, value, occurredAtUtc) in changed)
        {
            var previousValue = channel.CurrentValue;
            channel.CurrentValue = value;
            channel.ValueUpdatedAt = now;

            valueChanges.Add(new ChannelValueChange
            {
                IoChannelId = channel.Id,
                DeviceId = channel.DeviceId,
                ChannelNumber = channel.ChannelNumber,
                Value = value,
                UpdatedAt = now
            });

            notifications.Add(new ChannelChangedNotification
            {
                CabinetId = cabinetId,
                IoChannelId = channel.Id,
                DeviceId = channel.DeviceId,
                Direction = channel.Direction,
                ChannelNumber = channel.ChannelNumber,
                Value = value,
                PreviousValue = previousValue,
                TurnOn = ToLogical(polarities, channel.Id, value),
                OccurredAtUtc = occurredAtUtc,
                ReceivedAtUtc = now,
                Source = source
            });

            // Input ve Analaog input pinleri için her değişimde ChannelEvent(telemetri) yazılır.
            // Output pinleri için ChannelEvent YALNIZCA pinin değerini YANLIŞ BİLİYORSAK kaydedilir: karttan okuma (Sync), kayıtlı değerden
            // (son başarılı komut) farklı bir değer bildirdiyse röle komuttan sapmıştır (kart resetlendi, elle müdahale, delay'li komut).
            // Komutun kendisi buraya yazılmaz (kaydı DeviceCommand'dadır); kart son komutla aynı değeri bildiriyorsa hiçbir şey yazılmaz.
            // Kayıtlı değer yoksa (hiç komut görmemiş çıkış) bu da sapma DEĞİLDİR, değer ilk kez öğrenilir: yalnızca kanala yazılır.
            bool isInput = channel.Direction == PinDirection.Input || channel.Direction == PinDirection.AnalogInput;
            bool isOutputDrift = channel.Direction == PinDirection.Output && source == ChannelChangeSource.Sync && previousValue != null;

            if ((isInput || isOutputDrift) && value != null)
            {
                _unitOfWork.ChannelEvents.Add(new ChannelEvent
                {
                    IoChannelId = channel.Id,
                    CabinetId = cabinetId,
                    Value = value,
                    PreviousValue = previousValue,
                    OccurredAtUtc = occurredAtUtc,
                    ReceivedAtUtc = now,
                    Source = source
                });
            }

            if (isOutputDrift)
                _logger.LogWarning("Kabin {CabinetId}: {Address} çıkışı kayıtlı değerden farklı — Scadex {Previous}, kart {Value} bildirdi",
                    cabinetId, ScadaPinAddress.Format(channel.Direction, channel.ChannelNumber), previousValue ?? "bilinmiyor", value);
        }

        // Değişiklikler kalıcı olarak yazılır
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Kalıcı olarak yazılan değerler client'lara bildirilir
        if (valueChanges.Count > 0)
            await _notifier.ChannelValuesChangedAsync(cabinetId, valueChanges, cancellationToken);

        // Değişen kanallar Scadex'in dış modüllerine (dinleyicilere) yayınlanır
        foreach (var notification in notifications)
            await _observers.PublishAsync(o => o.OnChannelChangedAsync(notification, cancellationToken), _logger, nameof(IScadaEventObserver.OnChannelChangedAsync));

        return changed.Count;
    }

    /// <summary> Çıkışın fiziksel değeri → "yük açık mı". Giriş, bilinmeyen değer ya da çözülemeyen kutup <c>null</c>. </summary>
    private static bool? ToLogical(IReadOnlyDictionary<Guid, PolarityResolution> polarities, Guid ioChannelId, string? physicalValue)
    {
        if (!polarities.TryGetValue(ioChannelId, out var polarity) || !polarity.IsSuccess)
            return null;

        bool? physicalOn = physicalValue switch { "1" => true, "0" => false, _ => null };
        return physicalOn is bool on ? polarity.ToLogical(on) : null;
    }

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
