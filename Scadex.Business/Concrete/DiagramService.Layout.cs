using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;

namespace Scadex.Business.Concrete;

// Diyagramın YERLEŞİM ucu: sürükleme bitişi, hizala/dağıt, boyut, z-sırası, kilit, görünürlük. Diyagramın kalan tek toplu
// ucudur; her kayıt yalnızca kendi satırını güncellediği için kayıtlar arası tutarlılık ya da sıra sorusu doğmaz.
public partial class DiagramService
{
    public async Task<Result> UpdateLayoutAsync(Guid cabinetId, DiagramLayoutRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for DiagramLayoutRequest");

        if (request.Devices.Count == 0 && request.Annotations.Count == 0)
            return Result.Success();

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var deviceIds = request.Devices.Select(d => d.Id).ToList();
        var devices = deviceIds.Count == 0 ? [] : (await _unitOfWork.Devices.GetAllAsync(
            where: d => deviceIds.Contains(d.Id) && d.CabinetId == cabinetId && d.IsActive,
            cancellationToken: cancellationToken) ?? []).ToDictionary(d => d.Id);

        var annotationIds = request.Annotations.Select(a => a.Id).ToList();
        var annotations = annotationIds.Count == 0 ? [] : (await _unitOfWork.DiagramAnnotations.GetAllAsync(
            where: a => annotationIds.Contains(a.Id) && a.CabinetId == cabinetId,
            cancellationToken: cancellationToken) ?? []).ToDictionary(a => a.Id);

        // Bulunamayan kayit sessizce atlanmaz: yerlesimi yazilamayan bir kutu, kullanicinin tuvalde gordugunun
        // kaydedildigini sanmasi demek. Istemci 400'de sunucu gercegine doner.
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (int i = 0; i < request.Devices.Count; i++)
            if (!devices.ContainsKey(request.Devices[i].Id))
                AddError(errors, $"Devices[{i}].Id", "Cihaz bu kabinde bulunamadi");
        for (int i = 0; i < request.Annotations.Count; i++)
            if (!annotations.ContainsKey(request.Annotations[i].Id))
                AddError(errors, $"Annotations[{i}].Id", "Not bu kabinde bulunamadi");
        if (errors.Count > 0)
            return Result.Validation(ToFailures(errors), description: "Yerlesim kaydedilemedi");

        foreach (var item in request.Devices)
        {
            var device = devices[item.Id];
            device.CoordinateX = item.CoordinateX;
            device.CoordinateY = item.CoordinateY;
            device.Width = item.Width;
            device.Height = item.Height;
            device.Rotation = item.Rotation;
            device.ZIndex = item.ZIndex;
            device.IsLocked = item.IsLocked;
            device.IsVisible = item.IsVisible;
        }

        foreach (var item in request.Annotations)
        {
            var annotation = annotations[item.Id];
            annotation.CoordinateX = item.CoordinateX;
            annotation.CoordinateY = item.CoordinateY;
            annotation.Width = item.Width;
            annotation.Height = item.Height;
            annotation.Rotation = item.Rotation;
            annotation.ZIndex = item.ZIndex;
            annotation.IsLocked = item.IsLocked;
            annotation.IsVisible = item.IsVisible;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
