using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class DiagramAnnotationRepository : RepositoryBase<DiagramAnnotation, AppDbContext>, IDiagramAnnotationRepository
{
    public DiagramAnnotationRepository(AppDbContext context) : base(context)
    {
    }
}