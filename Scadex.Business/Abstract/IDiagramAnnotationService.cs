using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.DiagramAnnotation.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IDiagramAnnotationService
{
    // Get
    Task<Result<DiagramAnnotation>> GetAsync(Expression<Func<DiagramAnnotation, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<DiagramAnnotation>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DiagramAnnotationDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<DiagramAnnotation>>> GetListAsync(Expression<Func<DiagramAnnotation, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DiagramAnnotation>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DiagramAnnotationDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}
