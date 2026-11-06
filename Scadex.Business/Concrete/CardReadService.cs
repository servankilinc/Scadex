using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.ScadaObserver;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Realtime.Notifications;
using Scadex.Model.Realtime.Events;
using Scadex.Model.Scada.Commands;

namespace Scadex.Business.Concrete;

public class CardReadService : ICardReadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IEnumerable<IScadaEventObserver> _observers;
    private readonly IAutomationStatusService _automationStatusService;
    private readonly IDiagramNotifier _diagramNotifier;
    private readonly ILogger<CardReadService> _logger;

    public CardReadService(IUnitOfWork unitOfWork, IValidationService validationService, IEnumerable<IScadaEventObserver> observers, IAutomationStatusService automationStatusService, IDiagramNotifier diagramNotifier, ILogger<CardReadService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _observers = observers;
        _automationStatusService = automationStatusService;
        _diagramNotifier = diagramNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> IngestAsync(Guid automationId, ScadaCardReadRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ScadaCardReadRequest");


        // 2) Otomasyon, isteği gönderen kontrol kartının kimliğinden gelir (Basic Auth, controller).
        // 3) Otomasyon kontrolü
        var automation = await _unitOfWork.Automations.GetAsync(
            select: c => new { c.Id, c.IsActive },
            where: c => c.Id == automationId,
            cancellationToken: cancellationToken
        );
        if (automation == null || !automation.IsActive)
            return Result.NotFound(description: "Otomasyon bulunamadi veya pasif durumda");
        var card = await _unitOfWork.ControlCards.GetActiveForAutomationAsync(automation.Id, cancellationToken: cancellationToken);
        if (card == null)
            return Result.Failure($"Bu otomasyonda({automation.Id}) kontrol kartı yok.");
        if (!card.ScadaIsEnabled)
            return Result.Failure($"Bu otomasyonda({automation.Id}) SCADA kapalı.");

        // 3.1) Kart okuması SCADA kartını ile habeleşildiğinin kanıtıdır
        await _automationStatusService.RecordScadaContactAsync(automation.Id, cancellationToken);


        // 4) Kart -> aktif kullanici.
        var user = await _unitOfWork.Users.GetAsync(
            select: u => new { u.Id, u.FullName },
            where: u => u.IsActive && u.IdentityCardId == request.CardId,
            cancellationToken: cancellationToken
        );


        var now = DateTime.UtcNow;
        var occurredAtUtc = request.TimestampUtc ?? now;

        // 4.1) Otomasyonu izleyen ekranlara yayınlanır (Kart tanımlama için kullanılır)
        await _diagramNotifier.CardReadAsync(automation.Id, new CardReadNotify
        {
            AutomationId = automation.Id,
            CardId = request.CardId,
            UserId = user?.Id,
            UserFullName = user?.FullName,
            OccurredAtUtc = occurredAtUtc
        }, cancellationToken);


        // 5) Dinleyicilere yayınlanır. Dinleyen yoksa kart bilgisi — sessiz kalmasın diye loglanır
        if (!_observers.Any())
        {
            _logger.LogWarning($"Otomasyon {automation.Id}: kart id ({request.CardId}) alindi ama kayitli bir gozlemci yok; islenmedi.");
            return Result.Success();
        }

        var notification = new CardPresentedEvent
        {
            AutomationId = automation.Id,
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
