using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Dtos.Diagram.Commands;
using CabinetOs.Model.Dtos.Diagram.Queries;

namespace Scadex.Business.Utils.Diagram;

/// <summary>
/// YALNIZCA diyagram grafi. Palet okuma/yazarligi <c>IComponentTemplateService</c>'te,
/// canvas tercihleri <c>ICanvasSettingsService</c>'tedir — ikisi de baska aggregate'ler.
/// </summary>
public interface IDiagramService
{
    Task<Result<DiagramDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Basarida VERI DONMEZ (bos 200): diyagramdaki her satirin Guid'ini istemci
    /// urettigi icin geri ogrenilecek bir sey yok ve kaydetme atomik oldugundan
    /// 200 tek basina "gonderdigim her sey kalici" demektir.
    /// </summary>
    Task<Result> SaveAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken = default);
}
