using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

// DiagramService kaydetme boru hattinin NOT ailesi: yukleme -> dogrulama -> uygulama.
// Genel gerekce, akis haritasi ve SaveContext icin DiagramService.SaveContext.cs'e bakin.
public partial class DiagramService
{
    // ==================== YUKLEME ====================

    /// <summary>
    /// Gonderide gecen tum not Id'leri; TAKIPLI, kabin filtresi YOK.
    /// <c>DiagramAnnotation</c> ne aktiflestirilebilir ne soft-delete edilebilir,
    /// dolayisiyla "kaldirilmis" hali yoktur.
    /// </summary>
    private async Task<EntityLookup<DiagramAnnotation>> LoadAnnotationsAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var ids = request.DiagramAnnotations.Upserted.Select(a => a.Id).Concat(request.DiagramAnnotations.Deleted).Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _unitOfWork.DiagramAnnotations.GetAllAsync(
            where: a => ids.Contains(a.Id),
            cancellationToken: cancellationToken) ?? [];

        return Classify(rows, cabinetId, a => a.Id, a => a.CabinetId, _ => false);
    }

    // ==================== REFERANS DOGRULAMA ====================

    /// <summary>Not taslaklari: hedef satir erisilebilir mi.</summary>
    private static void ValidateAnnotations(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        for (int i = 0; i < request.DiagramAnnotations.Upserted.Count; i++)
        {
            ReportUnreachable(context.Annotations, request.DiagramAnnotations.Upserted[i].Id, errors, $"Annotations.Upserted[{i}].Id", "Not");
        }
    }

    // ==================== UYGULAMA ====================

    /// <summary>
    /// Sistemdeki TEK hard delete. <c>DiagramAnnotation</c> ne <c>IActivatableEntity</c>
    /// ne <c>ISoftDeletableEntity</c> oldugundan interceptor araya girmez ve satir
    /// gercekten silinir; kimse ona FK ile bagli olmadigi icin oksuz satir birakmaz.
    ///
    /// Silme sirasindaki yeri ve karsiligi bulunamayan Id'lerin neden sessizce
    /// atlandigi icin <c>ApplyDeletions</c>'a bakin (DiagramService.SaveContext.cs).
    /// </summary>
    private void ApplyAnnotationDeletions(DiagramSaveRequest request, SaveContext context)
    {
        var annotationsToRemove = request.DiagramAnnotations.Deleted
            .Where(context.Annotations.Live.ContainsKey)
            .Select(id => context.Annotations.Live[id])
            .ToList();
        if (annotationsToRemove.Count > 0)
            _unitOfWork.DiagramAnnotations.Delete(annotationsToRemove);
    }

    /// <summary>
    /// Not yazmalari. Her taslak icin tek soru: satir <c>Live</c> mi (guncelle) yoksa
    /// yok mu (olustur).
    /// </summary>
    private void ApplyAnnotationUpserts(Guid cabinetId, DiagramSaveRequest request, SaveContext context)
    {
        foreach (var draft in request.DiagramAnnotations.Upserted)
        {
            if (context.Annotations.Live.TryGetValue(draft.Id, out var annotation))
            {
                _mapper.Map(draft, annotation);
                continue;
            }

            annotation = new DiagramAnnotation { Id = draft.Id, CabinetId = cabinetId };
            _mapper.Map(draft, annotation);  // Id ve CabinetId disinda tum alanlar modelden kopyalanir
            _unitOfWork.DiagramAnnotations.Add(annotation);
        }
    }
}
