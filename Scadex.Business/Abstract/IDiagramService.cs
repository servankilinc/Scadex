using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Queries;

namespace Scadex.Business.Abstract;

public interface IDiagramService
{
    Task<Result<DiagramDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    Task<Result> SaveAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken = default);
}
