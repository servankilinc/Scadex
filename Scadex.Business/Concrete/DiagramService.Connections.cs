using Scadex.Business.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

// Diyagramın KABLO uçları: çizme ve görünüm formu. Silme DiagramService.Delete.cs'te.
public partial class DiagramService
{
    /// <summary>
    /// Kablo cizme. Iki uc da bu kabinde KALICI bir pin olmali — cihaz birakilinca aninda olusturuldugu icin kaydedilmemis
    /// pin yoktur ve uc tek kaynaktan cozulur.
    /// </summary>
    public async Task<Result> CreateConnectionAsync(Guid cabinetId, ConnectionCreateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ConnectionCreateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // ignoreFilters ZORUNLU: soft-delete edilmis satir birincil anahtar uzayini paylasmaya devam eder. Filtreli sorgu
        // onu "yok" gosterir ve INSERT PK ihlaliyle 500 doner.
        if (await _unitOfWork.Connections.IsExistAsync(where: c => c.Id == request.Id, ignoreFilters: true, cancellationToken: cancellationToken))
            AddError(errors, nameof(request.Id), "Bu kablo kimligi zaten kullanimda");

        var pinIds = new[] { request.SourcePinId, request.TargetPinId };
        var pins = (await _unitOfWork.Pins.GetAllAsync(
            select: p => new PinRow(p.Id, p.VoltageLevel),
            where: p => pinIds.Contains(p.Id) && p.Device!.CabinetId == cabinetId && p.Device.IsActive,
            cancellationToken: cancellationToken) ?? []).ToDictionary(p => p.Id);

        if (!pins.TryGetValue(request.SourcePinId, out var source))
            AddError(errors, nameof(request.SourcePinId), "Pin bu kabinde bulunamadi");
        if (!pins.TryGetValue(request.TargetPinId, out var target))
            AddError(errors, nameof(request.TargetPinId), "Pin bu kabinde bulunamadi");
        if (errors.Count > 0)
            return Result.Validation(ToFailures(errors), description: "Kablo cizilemedi");

        // Cift YONSUZ karsilastirilir. DB'deki unique index (SourcePinId, TargetPinId) sirali oldugu icin ters cizilmis ayni
        // kabloyu YAKALAMAZ; ConnectionMode.Loose ile "kaynak"/"hedef" zaten keyfi oldugundan burada daha katiyiz.
        var (sourcePinId, targetPinId) = (request.SourcePinId, request.TargetPinId);
        var pairTaken = await _unitOfWork.Connections.IsExistAsync(
            where: c => c.CabinetId == cabinetId
                     && ((c.SourcePinId == sourcePinId && c.TargetPinId == targetPinId) || (c.SourcePinId == targetPinId && c.TargetPinId == sourcePinId)),
            cancellationToken: cancellationToken);
        if (pairTaken)
            AddError(errors, "Pins", "Bu iki pin arasinda zaten bir kablo var");

        // Gerilim uyusmazligi: iki taraf da BELIRTILMISSE ve farkliysa reddedilir. Biri null ise ("belirtilmemis") susulur —
        // bilinmeyeni hata saymak, gerilimi henuz girilmemis sablonlarla calismayi imkansiz kilardi.
        if (source!.VoltageLevel.HasValue && target!.VoltageLevel.HasValue && source.VoltageLevel != target.VoltageLevel)
            AddError(errors, "Pins", "Farkli gerilim seviyesindeki pinler baglanamaz");

        if (errors.Count > 0)
            return Result.Validation(ToFailures(errors), description: "Kablo cizilemedi");

        var connection = new Connection
        {
            Id = request.Id,
            CabinetId = cabinetId,
            // Uclar YALNIZCA olusturmada yazilir; guncelleme istegi onlari tasimaz.
            SourcePinId = request.SourcePinId,
            TargetPinId = request.TargetPinId
        };
        WriteConnection(connection, request);
        await _unitOfWork.Connections.AddAndSaveAsync(connection, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdateConnectionAsync(Guid cabinetId, Guid connectionId, ConnectionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for ConnectionUpdateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        // Takipli okunur, Update() cagrilmaz: EF yalnizca degisen kolonlari yazar.
        var connection = await _unitOfWork.Connections.GetAsync(
            where: c => c.Id == connectionId && c.CabinetId == cabinetId,
            cancellationToken: cancellationToken);
        if (connection == null)
            return Result.NotFound(description: "Kablo bu kabinde bulunamadi");

        WriteConnection(connection, request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static void WriteConnection(Connection connection, ConnectionUpdateRequest request)
    {
        connection.Label = request.Label;
        connection.WireType = request.WireType;
        connection.Color = request.Color;
        connection.LineStyle = request.LineStyle;
        connection.StrokeWidth = request.StrokeWidth;
        connection.Routing = request.Routing;
        connection.WaypointsJson = DiagramWaypoints.Serialize(request.Waypoints);
        connection.ZIndex = request.ZIndex;
    }

    private sealed record PinRow(Guid Id, VoltageLevel? VoltageLevel);
}
