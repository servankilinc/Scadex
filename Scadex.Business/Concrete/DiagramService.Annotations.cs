using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

// Diyagramın NOT uçları: ekleme ve içerik formu. Konum / boyut / kilit DiagramService.Layout.cs'te, silme
// DiagramService.Delete.cs'te.
public partial class DiagramService
{
    public async Task<Result> CreateAnnotationAsync(Guid cabinetId, DiagramAnnotationCreateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for DiagramAnnotationCreateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        if (await _unitOfWork.DiagramAnnotations.IsExistAsync(where: a => a.Id == request.Id, cancellationToken: cancellationToken))
            return Result.Validation(new() { [nameof(request.Id)] = ["Bu not kimligi zaten kullanimda"] }, description: "Not eklenemedi");

        var annotation = new DiagramAnnotation { CabinetId = cabinetId };
        _mapper.Map(request, annotation);  // Id dahil; CabinetId disinda tum alanlar istekten kopyalanir
        await _unitOfWork.DiagramAnnotations.AddAndSaveAsync(annotation, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdateAnnotationAsync(Guid cabinetId, Guid annotationId, DiagramAnnotationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for DiagramAnnotationUpdateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var annotation = await _unitOfWork.DiagramAnnotations.GetAsync(
            where: a => a.Id == annotationId && a.CabinetId == cabinetId,
            cancellationToken: cancellationToken);
        if (annotation == null)
            return Result.NotFound(description: "Not bu kabinde bulunamadi");

        _mapper.Map(request, annotation);  // yalnizca icerik; yerlesim alanlari istekte yok, dokunulmaz
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
