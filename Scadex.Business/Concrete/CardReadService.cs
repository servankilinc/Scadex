using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.ScadaObserver;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Dtos.Scada.Events;

namespace Scadex.Business.Concrete;

public class CardReadService : ICardReadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IEnumerable<IScadaEventObserver> _observers;
    private readonly ICabinetStatusService _cabinetStatusService;
    private readonly IDiagramNotifier _diagramNotifier;
    private readonly ILogger<CardReadService> _logger;

    public CardReadService(IUnitOfWork unitOfWork, IValidationService validationService, IEnumerable<IScadaEventObserver> observers, ICabinetStatusService cabinetStatusService, IDiagramNotifier diagramNotifier, ILogger<CardReadService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _observers = observers;
        _cabinetStatusService = cabinetStatusService;
        _diagramNotifier = diagramNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> IngestAsync(Guid cabinetId, ScadaCardReadRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ScadaCardReadRequest");


        // 2) Kabin, isteği gönderen kontrol kartının kimliğinden gelir (Basic Auth, controller).
        // 3) Kabin kontrolü
        var cabinet = await _unitOfWork.Cabinets.GetAsync(
            select: c => new { c.Id, c.IsActive, c.ScadaIsEnabled },
            where: c => c.Id == cabinetId,
            cancellationToken: cancellationToken
        );
        if (cabinet == null || !cabinet.IsActive)
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");
        if (!cabinet.ScadaIsEnabled)
            return Result.Failure($"Bu kabinde({cabinet.Id}) SCADA kapalı.");

        // 3.1) Kart okuması SCADA kartını ile habeleşildiğinin kanıtıdır
        await _cabinetStatusService.RecordScadaContactAsync(cabinet.Id, cancellationToken);


        // 4) Kart -> aktif kullanici.
        var user = await _unitOfWork.Users.GetAsync(
            select: u => new { u.Id, u.FullName },
            where: u => u.IsActive && u.IdentityCardId == request.CardId,
            cancellationToken: cancellationToken
        );


        var now = DateTime.UtcNow;
        var occurredAtUtc = request.TimestampUtc ?? now;

        // 4.1) Kabini izleyen ekranlara yayınlanır (Kart tanımlama için kullanılır)
        await _diagramNotifier.CardReadAsync(cabinet.Id, new CardRead
        {
            CabinetId = cabinet.Id,
            CardId = request.CardId,
            UserId = user?.Id,
            UserFullName = user?.FullName,
            OccurredAtUtc = occurredAtUtc
        }, cancellationToken);


        // 5) Dinleyicilere yayınlanır. Dinleyen yoksa kart bilgisi — sessiz kalmasın diye loglanır
        if (!_observers.Any())
        {
            _logger.LogWarning($"Kabin {cabinet.Id}: kart id ({request.CardId}) alindi ama kayitli bir gozlemci yok; islenmedi.");
            return Result.Success();
        }

        var notification = new CardPresentedNotification
        {
            CabinetId = cabinet.Id,
            CardIdRaw = request.CardId,
            UserId = user?.Id,
            UserFullName = user?.FullName,
            OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = now
        };

        await _observers.PublishAsync(o => o.OnCardPresentedAsync(notification, cancellationToken), _logger, nameof(IScadaEventObserver.OnCardPresentedAsync));

        return Result.Success();
    }
}
