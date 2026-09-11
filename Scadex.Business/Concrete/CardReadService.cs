using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.ScadaEvents;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Dtos.Scada.Events;

namespace Scadex.Business.Concrete;

public class CardReadService : ICardReadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IEnumerable<IScadaEventObserver> _observers;
    private readonly ILogger<CardReadService> _logger;

    public CardReadService(IUnitOfWork unitOfWork, IValidationService validationService, IEnumerable<IScadaEventObserver> observers, ILogger<CardReadService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _observers = observers;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> IngestAsync(ScadaCardReadRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ScadaCardReadRequest");


        // 2) Scada kontrolü
        var cabinetId = await _unitOfWork.Devices.GetCabinetIdByControlModuleMacAsync(request.MacAddress, cancellationToken);
        if (cabinetId == Guid.Empty)
        {
            _logger.LogWarning($"MAC {request.MacAddress}: eslesen kontrol modulu yok (ya da pasif); kart okumasi atlandi.");
            return Result.NotFound(description: "Bu MAC adresine kayitli kontrol modulu bulunamadi");
        }


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


        // 4) Kart -> aktif kullanici.
        var user = await _unitOfWork.Users.GetAsync(
            select: u => new { u.Id, u.FullName },
            where: u => u.IsActive && u.IdentityCardId == request.CardId,
            cancellationToken: cancellationToken
        );


        // 5) Dinleyicilere yayınlanır. Dinleyen yoksa kart bilgisi — sessiz kalmasın diye loglanır
        if (!_observers.Any())
        {
            _logger.LogWarning($"Kabin {cabinet.Id}: kart id ({request.CardId}) alindi ama kayitli bir gozlemci yok; islenmedi.");
            return Result.Success();
        }

        var now = DateTime.UtcNow;
        var notification = new CardPresentedNotification
        {
            CabinetId = cabinet.Id,
            CardIdRaw = request.CardId,
            UserId = user?.Id,
            UserFullName = user?.FullName,
            OccurredAtUtc = request.TimestampUtc ?? now,
            ReceivedAtUtc = now
        };

        await _observers.PublishAsync(o => o.OnCardPresentedAsync(notification, cancellationToken), _logger, nameof(IScadaEventObserver.OnCardPresentedAsync));

        return Result.Success();
    }
}
