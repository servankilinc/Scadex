using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IDiagramAnnotationRepository : IRepository<DiagramAnnotation>, IRepositoryAsync<DiagramAnnotation>
{
}